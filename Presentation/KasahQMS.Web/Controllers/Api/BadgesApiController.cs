using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KasahQMS.Web.Controllers.Api;

/// <summary>
/// API for getting badge counts for real-time UI updates.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BadgesApiController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IMemoryCache _cache;

    public BadgesApiController(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IHierarchyService hierarchyService, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _cache = cache;
    }

    /// <summary>
    /// Get all badge counts for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBadges()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        var isExecutive = currentUser?.Roles?.Any(r =>
            r.Name is "TMD" or "TopManagingDirector" or "Country Manager" or "Deputy" or "DeputyDirector" or "Deputy Country Manager") == true;

        // Unread messages - count threads with unread messages for this user
        var lastSeenMessages = _cache.Get<DateTime?>($"last_seen_messages_{userId}");
        var unreadMessages = await GetUnreadMessagesCountAsync(userId.Value, tenantId, lastSeenMessages);

        // New tasks: assigned to user (open or in progress), created/updated after last seen tasks
        var lastSeenTasks = _cache.Get<DateTime?>($"last_seen_tasks_{userId}");
        var pendingTasksQuery = _dbContext.QmsTasks
            .Where(t => t.TenantId == tenantId && 
                        t.AssignedToId == userId.Value && 
                        (t.Status == QmsTaskStatus.Open || t.Status == QmsTaskStatus.InProgress));
        var pendingTasks = lastSeenTasks.HasValue
            ? await pendingTasksQuery.CountAsync(t => t.CreatedAt > lastSeenTasks.Value || t.LastModifiedAt > lastSeenTasks.Value)
            : await pendingTasksQuery.CountAsync();

        // Training updates badge (changes in ongoing training visible to creator/trainer/trainee)
        var lastSeenTraining = _cache.Get<DateTime?>($"last_seen_training_{userId}");
        var trainingQuery = _dbContext.TrainingRecords
            .Where(t => t.TenantId == tenantId &&
                        t.Status != TrainingStatus.Expired &&
                        (t.UserId == userId.Value || t.TrainerId == userId.Value || t.CreatedById == userId.Value));
        var pendingTraining = lastSeenTraining.HasValue
            ? await trainingQuery.CountAsync(t => t.CreatedAt > lastSeenTraining.Value ||
                                                  (t.LastModifiedAt.HasValue && t.LastModifiedAt.Value > lastSeenTraining.Value))
            : await trainingQuery.CountAsync();

        // Documents pending review (current approver is user) or sent to user
        var pendingDocuments = await _dbContext.Documents
            .CountAsync(d => d.TenantId == tenantId &&
                            (
                              (isExecutive && (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview)) ||
                              (d.CurrentApproverId == userId.Value &&
                               (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview)) ||
                              (d.TargetUserId == userId.Value && d.Status == DocumentStatus.Draft) ||
                              (d.CreatedById == userId.Value &&
                               d.Status == DocumentStatus.Draft &&
                               _dbContext.DocumentApprovals.Any(a => a.DocumentId == d.Id && !a.IsApproved))
                            ));

        // Unread notifications
        var unreadNotifications = await _dbContext.Notifications
            .CountAsync(n => n.UserId == userId.Value && !n.IsRead);

        // Pending Approvals (aggregate for Approvals page badge)
        var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(userId.Value, recursive: true);
        var subordinateIdsList = subordinateIds.ToList();
        var pendingTaskApprovals = await _dbContext.QmsTasks
            .CountAsync(t => t.TenantId == tenantId && 
                            t.Status == QmsTaskStatus.AwaitingApproval &&
                            (subordinateIdsList.Contains(t.CreatedById) || 
                             (t.AssignedToId.HasValue && subordinateIdsList.Contains(t.AssignedToId.Value))));

        var pendingDocumentApprovals = await _dbContext.Documents
            .CountAsync(d => d.TenantId == tenantId &&
                            d.Status == DocumentStatus.Submitted &&
                            d.CurrentApproverId == userId.Value);

        var pendingTrainingApprovals = await _dbContext.TrainingRecords
            .CountAsync(t => t.TenantId == tenantId &&
                            t.Status == TrainingStatus.Completed &&
                            t.CreatedById == userId.Value);

        var pendingApprovals = pendingTaskApprovals + pendingDocumentApprovals + pendingTrainingApprovals;

        return Ok(new BadgeCounts(unreadMessages, pendingTasks, pendingDocuments, unreadNotifications, pendingApprovals, pendingTraining));
    }

    /// <summary>
    /// Get conversations with unread messages.
    /// </summary>
    [HttpGet("unread-chats")]
    public async Task<IActionResult> GetUnreadChats()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Get threads where user is participant or department member
        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        var userOrgUnitId = user?.OrganizationUnitId;

        // Get direct threads where user is participant
        var participantThreadIds = await _dbContext.ChatThreadParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId.Value)
            .Select(p => p.ThreadId)
            .ToListAsync();

        // Get department threads
        var deptThreadIds = userOrgUnitId.HasValue 
            ? await _dbContext.ChatThreads
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && 
                           t.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department && 
                           t.OrganizationUnitId == userOrgUnitId)
                .Select(t => t.Id)
                .ToListAsync()
            : new List<Guid>();

        var allThreadIds = participantThreadIds.Concat(deptThreadIds).Distinct().ToList();

        // Get threads with unread messages (messages after user's last read or messages not by user)
        var unreadChats = new List<UnreadChatInfo>();
        foreach (var threadId in allThreadIds)
        {
            var latestMessage = await _dbContext.ChatMessages
                .AsNoTracking()
                .Where(m => m.ThreadId == threadId && !m.IsDeleted && m.SenderId != userId.Value)
                .OrderByDescending(m => m.CreatedAt)
                .Include(m => m.Sender)
                .FirstOrDefaultAsync();

            if (latestMessage != null)
            {
                var thread = await _dbContext.ChatThreads.AsNoTracking()
                    .Include(t => t.OrganizationUnit)
                    .FirstOrDefaultAsync(t => t.Id == threadId);

                if (thread != null)
                {
                    var name = thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department
                        ? thread.OrganizationUnit?.Name ?? "Department"
                        : latestMessage.Sender != null 
                            ? $"{latestMessage.Sender.FirstName} {latestMessage.Sender.LastName}"
                            : "Unknown";

                    unreadChats.Add(new UnreadChatInfo(
                        threadId,
                        name,
                        thread.Type.ToString(),
                        latestMessage.Content.Length > 50 ? latestMessage.Content.Substring(0, 47) + "..." : latestMessage.Content,
                        latestMessage.CreatedAt,
                        latestMessage.SenderId
                    ));
                }
            }
        }

        return Ok(unreadChats.OrderByDescending(c => c.LastMessageAt).Take(10));
    }

    /// <summary>
    /// Mark a badge type as seen, resetting its count.
    /// </summary>
    [HttpPost("mark-seen/{type}")]
    public IActionResult MarkSeen(string type)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var expiry = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) };
        if (type == "tasks")
            _cache.Set($"last_seen_tasks_{userId}", (DateTime?)DateTime.UtcNow, expiry);
        else if (type == "messages")
            _cache.Set($"last_seen_messages_{userId}", (DateTime?)DateTime.UtcNow, expiry);
        else if (type == "training")
            _cache.Set($"last_seen_training_{userId}", (DateTime?)DateTime.UtcNow, expiry);

        return Ok();
    }

    private async Task<int> GetUnreadMessagesCountAsync(Guid userId, Guid tenantId, DateTime? lastSeen = null)
    {
        // Get threads where user is participant
        var participantThreadIds = await _dbContext.ChatThreadParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ThreadId)
            .ToListAsync();

        // Count unread messages (from others) in those threads
        var query = _dbContext.ChatMessages
            .Where(m => participantThreadIds.Contains(m.ThreadId) && 
                        !m.IsDeleted && 
                        m.SenderId != userId);

        if (lastSeen.HasValue)
            query = query.Where(m => m.CreatedAt > lastSeen.Value);

        var count = await query.CountAsync();
        return Math.Min(count, 99); // Cap at 99 for display
    }
}

public record BadgeCounts(int UnreadMessages, int PendingTasks, int PendingDocuments, int UnreadNotifications, int PendingApprovals, int PendingTraining);
public record UnreadChatInfo(Guid ThreadId, string Name, string Type, string LastMessage, DateTime LastMessageAt, Guid SenderId);
