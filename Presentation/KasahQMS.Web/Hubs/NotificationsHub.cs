using System.Security.Claims;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    public const string Path = "/hubs/notifications";
    
    private readonly ApplicationDbContext _db;
    
    public NotificationsHub(ApplicationDbContext db)
    {
        _db = db;
    }

    private Guid? GetUserId()
    {
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var g) ? g : null;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        
        // Send initial unread count
        var unreadCount = await _db.Notifications
            .CountAsync(n => n.UserId == userId.Value && !n.IsRead);
        await Clients.Caller.SendAsync("UnreadCount", unreadCount);
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != null)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        await base.OnDisconnectedAsync(exception);
    }
    
    /// <summary>Mark a single notification as read</summary>
    public async Task MarkAsRead(Guid notificationId)
    {
        var userId = GetUserId();
        if (userId == null) return;
        
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId.Value);
        if (notification == null) return;
        
        notification.MarkAsRead();
        await _db.SaveChangesAsync();
        
        var unreadCount = await _db.Notifications
            .CountAsync(n => n.UserId == userId.Value && !n.IsRead);
        await Clients.Caller.SendAsync("UnreadCount", unreadCount);
    }
    
    /// <summary>Mark all notifications as read</summary>
    public async Task MarkAllAsRead()
    {
        var userId = GetUserId();
        if (userId == null) return;
        
        var unreadNotifications = await _db.Notifications
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ToListAsync();
        
        foreach (var n in unreadNotifications)
        {
            n.MarkAsRead();
        }
        
        await _db.SaveChangesAsync();
        await Clients.Caller.SendAsync("UnreadCount", 0);
    }
    
    /// <summary>Get recent notifications</summary>
    public async Task GetRecent(int limit = 10)
    {
        var userId = GetUserId();
        if (userId == null) return;
        
        var notifications = await _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                Type = n.Type.ToString(),
                n.RelatedEntityId,
                n.RelatedEntityType,
                n.IsRead,
                n.CreatedAt
            })
            .ToListAsync();
        
        await Clients.Caller.SendAsync("RecentNotifications", notifications);
    }
}
