using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using KasahQMS.Domain.Entities.Configuration;

namespace KasahQMS.Web.Controllers.Api;

/// <summary>
/// API for getting badge counts for real-time UI updates.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BadgesApiController : ControllerBase
{
    private const string LastSeenSettingPrefix = "badge.last_seen";
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
        var badgeCacheKey = $"badge_counts_{tenantId}_{userId.Value}";
        if (_cache.TryGetValue(badgeCacheKey, out BadgeCounts? cachedBadges) && cachedBadges is not null)
        {
            return Ok(cachedBadges);
        }

        var executiveRoles = new[]
        {
            "TMD",
            "TopManagingDirector",
            "Country Manager",
            "Deputy",
            "DeputyDirector",
            "Deputy Country Manager"
        };

        var isExecutive = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId.Value)
            .SelectMany(u => u.Roles)
            .AnyAsync(r => executiveRoles.Contains(r.Name));

        // Unread messages - persisted across sessions.
        var lastSeenMessages = await GetPersistedLastSeenAsync("messages", userId.Value, tenantId);
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
                            (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview) &&
                            d.CurrentApproverId == userId.Value);

        var pendingTrainingCandidates = await _dbContext.TrainingRecords
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                        t.Status == TrainingStatus.Completed &&
                        t.CreatedById == userId.Value)
            .Select(t => t.Notes)
            .ToListAsync();
        var pendingTrainingApprovals = pendingTrainingCandidates.Count(IsPendingTrainingApproval);

        var pendingApprovals = pendingTaskApprovals + pendingDocumentApprovals + pendingTrainingApprovals;

        var badges = new BadgeCounts(unreadMessages, pendingTasks, pendingDocuments, unreadNotifications, pendingApprovals, pendingTraining);
        _cache.Set(badgeCacheKey, badges, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
        });

        return Ok(badges);
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
        if (allThreadIds.Count == 0)
        {
            return Ok(Array.Empty<UnreadChatInfo>());
        }

        var latestMessages = await _dbContext.ChatMessages
            .AsNoTracking()
            .Where(m => allThreadIds.Contains(m.ThreadId) && !m.IsDeleted && m.SenderId != userId.Value)
            .GroupBy(m => m.ThreadId)
            .Select(g => g
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new
                {
                    m.ThreadId,
                    m.Content,
                    m.CreatedAt,
                    m.SenderId,
                    SenderName = m.Sender != null
                        ? $"{m.Sender.FirstName} {m.Sender.LastName}"
                        : "Unknown"
                })
                .FirstOrDefault())
            .Where(m => m != null)
            .ToListAsync();

        var threadLookup = await _dbContext.ChatThreads
            .AsNoTracking()
            .Where(t => allThreadIds.Contains(t.Id))
            .Select(t => new
            {
                t.Id,
                t.Type,
                DepartmentName = t.OrganizationUnit != null ? t.OrganizationUnit.Name : null
            })
            .ToDictionaryAsync(t => t.Id);

        var unreadChats = new List<UnreadChatInfo>(latestMessages.Count);
        foreach (var latestMessage in latestMessages)
        {
            if (latestMessage == null || !threadLookup.TryGetValue(latestMessage.ThreadId, out var thread))
            {
                continue;
            }

            var name = thread.Type == KasahQMS.Domain.Entities.Chat.ChatThreadType.Department
                ? thread.DepartmentName ?? "Department"
                : latestMessage.SenderName;

            unreadChats.Add(new UnreadChatInfo(
                latestMessage.ThreadId,
                name,
                thread.Type.ToString(),
                latestMessage.Content.Length > 50 ? latestMessage.Content[..47] + "..." : latestMessage.Content,
                latestMessage.CreatedAt,
                latestMessage.SenderId
            ));
        }

        return Ok(unreadChats.OrderByDescending(c => c.LastMessageAt).Take(10));
    }

    /// <summary>
    /// Mark a badge type as seen, resetting its count.
    /// </summary>
    [HttpPost("mark-seen/{type}")]
    public async Task<IActionResult> MarkSeen(string type)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();
        var tenantId = _currentUserService.TenantId;
        if (!tenantId.HasValue) return Unauthorized();

        var expiry = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) };
        if (type == "tasks")
            _cache.Set($"last_seen_tasks_{userId}", (DateTime?)DateTime.UtcNow, expiry);
        else if (type == "messages")
        {
            _cache.Set($"last_seen_messages_{userId}", (DateTime?)DateTime.UtcNow, expiry);
            await SetPersistedLastSeenAsync("messages", userId.Value, tenantId.Value, DateTime.UtcNow);
        }
        else if (type == "training")
            _cache.Set($"last_seen_training_{userId}", (DateTime?)DateTime.UtcNow, expiry);

        if (tenantId.HasValue)
        {
            _cache.Remove($"badge_counts_{tenantId.Value}_{userId.Value}");
        }

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

    private async Task<DateTime?> GetPersistedLastSeenAsync(string type, Guid userId, Guid tenantId)
    {
        var key = $"{LastSeenSettingPrefix}.{type}.{userId:N}";
        var value = await _dbContext.SystemSettings
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }

    private async Task SetPersistedLastSeenAsync(string type, Guid userId, Guid tenantId, DateTime value)
    {
        var key = $"{LastSeenSettingPrefix}.{type}.{userId:N}";
        var setting = await _dbContext.SystemSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key);

        if (setting == null)
        {
            _dbContext.SystemSettings.Add(SystemSetting.Create(
                tenantId,
                key,
                value.ToString("o"),
                userId,
                $"Last seen timestamp for {type} badge"));
        }
        else
        {
            setting.Value = value.ToString("o");
        }

        await _dbContext.SaveChangesAsync();
    }

    private static bool IsPendingTrainingApproval(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return true;
        try
        {
            using var doc = JsonDocument.Parse(notes);
            var root = doc.RootElement;
            var hasDecision = root.TryGetProperty("CreatorDecision", out var decision) &&
                              !string.IsNullOrWhiteSpace(decision.GetString());
            var isArchived = root.TryGetProperty("IsArchived", out var archived) &&
                             archived.ValueKind == JsonValueKind.True;
            return !hasDecision && !isArchived;
        }
        catch (JsonException)
        {
            return true;
        }
    }
}

public record BadgeCounts(int UnreadMessages, int PendingTasks, int PendingDocuments, int UnreadNotifications, int PendingApprovals, int PendingTraining);
public record UnreadChatInfo(Guid ThreadId, string Name, string Type, string LastMessage, DateTime LastMessageAt, Guid SenderId);
