using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public NotificationsApiController(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 20, [FromQuery] bool unreadOnly = false, [FromQuery] DateTime? before = null)
    {
        var uid = _currentUser.UserId ?? (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null);
        if (uid == null)
            return Unauthorized();

        limit = Math.Clamp(limit, 1, 100);

        IQueryable<Notification> q = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == uid.Value)
            .OrderByDescending(n => n.CreatedAt);
        if (unreadOnly)
            q = q.Where(n => !n.IsRead);
        if (before.HasValue)
            q = q.Where(n => n.CreatedAt < before.Value);

        var items = await q.Take(limit + 1).Select(n => new
        {
            n.Id,
            n.Title,
            n.Message,
            Type = n.Type.ToString(),
            n.RelatedEntityId,
            n.RelatedEntityType,
            n.IsRead,
            n.CreatedAt
        }).ToListAsync();

        var hasMore = items.Count > limit;
        if (hasMore)
            items.RemoveAt(items.Count - 1);

        var nextBefore = items.LastOrDefault()?.CreatedAt;

        var unreadCount = await _db.Notifications
            .CountAsync(n => n.UserId == uid.Value && !n.IsRead);

        return Ok(new { unreadCount, items, hasMore, nextBefore });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var uid = _currentUser.UserId ?? (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null);
        if (uid == null)
            return Unauthorized();

        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid.Value);
        if (n == null) return NotFound();

        n.MarkAsRead();
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = _currentUser.UserId ?? (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null);
        if (uid == null)
            return Unauthorized();

        var unread = await _db.Notifications
            .Where(n => n.UserId == uid.Value && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.MarkAsRead();
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
