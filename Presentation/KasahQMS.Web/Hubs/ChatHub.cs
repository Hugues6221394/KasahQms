using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Chat;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Hubs;

[Authorize]
public class ChatHub : Hub
{
    public const string Path = "/hubs/chat";

    private readonly IChatService _chatService;
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationsHub> _notificationsHub;

    public ChatHub(IChatService chatService, ApplicationDbContext db, IHubContext<NotificationsHub> notificationsHub)
    {
        _chatService = chatService;
        _db = db;
        _notificationsHub = notificationsHub;
    }

    private Guid? UserId()
    {
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var g) ? g : null;
    }

    private async Task<Guid?> TenantIdAsync()
    {
        var uid = UserId();
        if (uid == null) return null;
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid.Value);
        return u?.TenantId;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = UserId();
        if (userId == null) { Context.Abort(); return; }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = UserId();
        if (userId != null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Join department group. User must be in that org unit.</summary>
    public async Task JoinDepartment(Guid orgUnitId)
    {
        var userId = UserId();
        var tenantId = await TenantIdAsync();
        if (userId == null || tenantId == null) return;

        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
        if (u?.OrganizationUnitId != orgUnitId) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"dept:{orgUnitId}");
    }

    /// <summary>Leave department group.</summary>
    public async Task LeaveDepartment(Guid orgUnitId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dept:{orgUnitId}");
    }

    /// <summary>Join thread group. User must be participant or in thread's org unit(s).</summary>
    public async Task JoinThread(Guid threadId)
    {
        var userId = UserId();
        var tenantId = await TenantIdAsync();
        if (userId == null || tenantId == null) return;

        var t = await _db.ChatThreads
            .AsNoTracking()
            .Include(x => x.Participants)
            .FirstOrDefaultAsync(x => x.Id == threadId && x.TenantId == tenantId);

        if (t == null) return;

        var allowed = false;
        if (t.Type == ChatThreadType.Direct && t.Participants != null)
            allowed = t.Participants.Any(p => p.UserId == userId);
        else if (t.Type == ChatThreadType.Department && t.OrganizationUnitId.HasValue)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            allowed = u?.OrganizationUnitId == t.OrganizationUnitId;
        }
        else if (t.Type == ChatThreadType.CrossDept && (t.OrganizationUnitId.HasValue || t.SecondOrganizationUnitId.HasValue))
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            allowed = u != null && (u.OrganizationUnitId == t.OrganizationUnitId || u.OrganizationUnitId == t.SecondOrganizationUnitId);
        }

        if (!allowed) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"thread:{threadId}");
    }

    /// <summary>Leave thread group.</summary>
    public async Task LeaveThread(Guid threadId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"thread:{threadId}");
    }

    /// <summary>Send message to department chat.</summary>
    public async Task SendToDepartment(Guid orgUnitId, string message)
    {
        var userId = UserId();
        var tenantId = await TenantIdAsync();
        if (userId == null || tenantId == null) return;

        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
        // Allow managers/executives to send to any department, others only to their own
        var userRoles = await _db.Users.AsNoTracking()
            .Where(x => x.Id == userId.Value)
            .SelectMany(x => x.Roles)
            .Select(r => r.Name)
            .ToListAsync();
        var isManagerOrExec = userRoles.Any(r => r == "TMD" || r == "Deputy Country Manager" || r == "Department Manager" || r == "System Admin");
        if (!isManagerOrExec && u?.OrganizationUnitId != orgUnitId) return;

        var thread = await _chatService.GetOrCreateDepartmentThreadAsync(tenantId.Value, orgUnitId, userId.Value);
        if (thread == null) return;

        var dto = await _chatService.SendMessageAsync(thread.Id, userId.Value, message ?? "");
        if (dto == null) return;

        var payload = new
        {
            dto.Id,
            dto.ThreadId,
            dto.SenderId,
            dto.SenderName,
            dto.Content,
            dto.CreatedAt
        };

        // Send to department group
        await Clients.Group($"dept:{orgUnitId}").SendAsync("ReceiveMessage", payload);
        
        // Also send to all users in the department via their user groups
        var deptUsers = await _db.Users.AsNoTracking()
            .Where(x => x.OrganizationUnitId == orgUnitId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync();
        foreach (var uid in deptUsers)
        {
            await Clients.Group($"user:{uid}").SendAsync("ReceiveMessage", payload);
            // Send notification to all dept users except sender
            if (uid != userId.Value)
            {
                await _notificationsHub.Clients.Group($"user:{uid}").SendAsync("NewMessage", new
                {
                    ThreadId = thread.Id,
                    SenderId = dto.SenderId,
                    SenderName = dto.SenderName,
                    Content = dto.Content?.Length > 50 ? dto.Content.Substring(0, 47) + "..." : dto.Content
                });
            }
        }
    }

    /// <summary>Send message to a thread (direct, dept, or cross-dept).</summary>
    public async Task SendToThread(Guid threadId, string message)
    {
        var userId = UserId();
        var tenantId = await TenantIdAsync();
        if (userId == null || tenantId == null) return;

        var t = await _db.ChatThreads.AsNoTracking().Include(x => x.Participants).FirstOrDefaultAsync(x => x.Id == threadId && x.TenantId == tenantId);
        if (t == null) return;

        var allowed = false;
        if (t.Type == ChatThreadType.Direct && t.Participants != null)
            allowed = t.Participants.Any(p => p.UserId == userId);
        else if (t.Type == ChatThreadType.Department && t.OrganizationUnitId.HasValue)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            allowed = u?.OrganizationUnitId == t.OrganizationUnitId;
        }
        else if (t.Type == ChatThreadType.CrossDept)
        {
            var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId.Value);
            allowed = u != null && (u.OrganizationUnitId == t.OrganizationUnitId || u.OrganizationUnitId == t.SecondOrganizationUnitId);
        }

        if (!allowed) return;

        var dto = await _chatService.SendMessageAsync(threadId, userId.Value, message ?? "");
        if (dto == null) return;

        await Clients.Group($"thread:{threadId}").SendAsync("ReceiveMessage", new
        {
            dto.Id,
            dto.ThreadId,
            dto.SenderId,
            dto.SenderName,
            dto.Content,
            dto.CreatedAt
        });
    }

    /// <summary>Send direct message to another user. Creates or reuses a direct thread.</summary>
    public async Task SendToUser(Guid targetUserId, string message, Guid? taskId = null)
    {
        var userId = UserId();
        var tenantId = await TenantIdAsync();
        if (userId == null || tenantId == null || targetUserId == userId) return;

        var thread = await _chatService.GetOrCreateDirectThreadAsync(tenantId.Value, userId.Value, targetUserId, taskId);
        if (thread == null) return;

        var dto = await _chatService.SendMessageAsync(thread.Id, userId.Value, message ?? "");
        if (dto == null) return;

        var payload = new
        {
            dto.Id,
            dto.ThreadId,
            dto.SenderId,
            dto.SenderName,
            dto.Content,
            dto.CreatedAt
        };

        // Send to thread group (for users who joined)
        await Clients.Group($"thread:{thread.Id}").SendAsync("ReceiveMessage", payload);
        
        // Also send to both user groups to ensure they receive it even if not in thread group
        await Clients.Group($"user:{userId}").SendAsync("ReceiveMessage", payload);
        await Clients.Group($"user:{targetUserId}").SendAsync("ReceiveMessage", payload);

        // Send real-time notification to recipient (for badge update and toast)
        await _notificationsHub.Clients.Group($"user:{targetUserId}").SendAsync("NewMessage", new
        {
            ThreadId = thread.Id,
            SenderId = dto.SenderId,
            SenderName = dto.SenderName,
            Content = dto.Content?.Length > 50 ? dto.Content.Substring(0, 47) + "..." : dto.Content
        });
    }

    /// <summary>Notify a user of a new message (for badge updates).</summary>
    private async Task NotifyNewMessageAsync(Guid recipientUserId, Guid threadId, string senderName, string content)
    {
        await _notificationsHub.Clients.Group($"user:{recipientUserId}").SendAsync("NewMessage", new
        {
            ThreadId = threadId,
            SenderName = senderName,
            Content = content?.Length > 50 ? content.Substring(0, 47) + "..." : content
        });
    }
}
