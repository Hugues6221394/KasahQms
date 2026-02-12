using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Notifications;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<NotificationItem> Notifications { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null) return;

        Notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationItem(
                n.Id,
                n.Title,
                n.Message,
                n.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                n.IsRead,
                n.Type.ToString(),
                n.RelatedEntityId,
                n.RelatedEntityType
            ))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostMarkAllAsReadAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Unauthorized();

        var unread = await _dbContext.Notifications
            .Where(n => n.UserId == userId.Value && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.MarkAsRead();
        }

        await _dbContext.SaveChangesAsync();
        return RedirectToPage();
    }

    public record NotificationItem(
        Guid Id, 
        string Title, 
        string Message, 
        string Time, 
        bool IsRead, 
        string Type, 
        Guid? RelatedEntityId, 
        string? RelatedEntityType);
}
