using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    public BadgesApiController(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IHierarchyService hierarchyService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
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

        // Unread messages - count threads with unread messages for this user
        var unreadMessages = await GetUnreadMessagesCountAsync(userId.Value, tenantId);

        // Pending tasks assigned to user (open or in progress)
        var pendingTasks = await _dbContext.QmsTasks
            .CountAsync(t => t.TenantId == tenantId && 
                            t.AssignedToId == userId.Value && 
                            (t.Status == QmsTaskStatus.Open || t.Status == QmsTaskStatus.InProgress));

        // Documents pending review (current approver is user) or sent to user
        var pendingDocuments = await _dbContext.Documents
            .CountAsync(d => d.TenantId == tenantId &&
                            ((d.CurrentApproverId == userId.Value && 
                              (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview)) ||
                             (d.TargetUserId == userId.Value && d.Status == DocumentStatus.Draft)));

        // Unread notifications
        var unreadNotifications = await _dbContext.Notifications
            .CountAsync(n => n.UserId == userId.Value && !n.IsRead);

        // Pending Approvals (Tasks from subordinates)
        var subordinateIds = await _hierarchyService.GetSubordinateIdsAsync(userId.Value, recursive: true);
        var subordinateIdsList = subordinateIds.ToList();
        var pendingApprovals = await _dbContext.QmsTasks
            .CountAsync(t => t.TenantId == tenantId && 
                            t.Status == QmsTaskStatus.AwaitingApproval &&
                            (subordinateIdsList.Contains(t.CreatedById) || 
                             (t.AssignedToId.HasValue && subordinateIdsList.Contains(t.AssignedToId.Value))));

        return Ok(new BadgeCounts(unreadMessages, pendingTasks, pendingDocuments, unreadNotifications, pendingApprovals));
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

    private async Task<int> GetUnreadMessagesCountAsync(Guid userId, Guid tenantId)
    {
        // Get threads where user is participant
        var participantThreadIds = await _dbContext.ChatThreadParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ThreadId)
            .ToListAsync();

        // Count unread messages (from others) in those threads
        var count = await _dbContext.ChatMessages
            .CountAsync(m => participantThreadIds.Contains(m.ThreadId) && 
                            !m.IsDeleted && 
                            m.SenderId != userId);

        return Math.Min(count, 99); // Cap at 99 for display
    }
}

public record BadgeCounts(int UnreadMessages, int PendingTasks, int PendingDocuments, int UnreadNotifications, int PendingApprovals);
public record UnreadChatInfo(Guid ThreadId, string Name, string Type, string LastMessage, DateTime LastMessageAt, Guid SenderId);
