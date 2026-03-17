using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AppAuthService = KasahQMS.Application.Common.Security.IAuthorizationService;

namespace KasahQMS.Web.Pages.News;

[Microsoft.AspNetCore.Authorization.Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly AppAuthService _authorizationService;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        AppAuthService authorizationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
    }

    public List<NewsRow> Articles { get; set; } = new();
    public int UnreadCount { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public Guid? CurrentUserId { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        CurrentUserId = userId;
        var tenantId = _currentUserService.TenantId 
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null || tenantId == default)
        {
            Articles = new();
            return;
        }

        // Check if user can create/edit news
        CanCreate = await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Create);
        CanEdit = await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Edit);

        // Get all active news for this tenant
        var query = _dbContext.NewsArticles.AsNoTracking()
            .Include(n => n.PublishedBy)
            .Where(n => n.TenantId == tenantId && n.IsActive)
            .OrderByDescending(n => n.PublishedAt);

        Articles = await query
            .Select(n => new NewsRow(
                n.Id,
                n.Title,
                n.Type.ToString(),
                GetTypeBadgeClass(n.Type),
                n.Priority.ToString(),
                GetPriorityBadgeClass(n.Priority),
                n.PublishedBy != null ? n.PublishedBy.FirstName + " " + n.PublishedBy.LastName : "—",
                n.PublishedAt.ToString("MMM dd, yyyy HH:mm"),
                n.ReadBy.Any(r => r.UserId == userId),
                n.PublishedById
            ))
            .ToListAsync();

        // Count unread articles
        UnreadCount = Articles.Count(a => !a.IsRead);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == default)
            return RedirectToPage();

        var article = await _dbContext.NewsArticles
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);

        if (article == null)
            return NotFound();

        // Only creator or users with Edit permission can delete
        var canEdit = await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Edit);
        if (!canEdit && article.PublishedById != userId)
            return RedirectToPage("/Account/AccessDenied");

        // Soft delete
        article.IsActive = false;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage();
    }

    private static string GetTypeBadgeClass(NewsType type) => type switch
    {
        NewsType.Alert => "bg-orange-100 text-orange-700",
        NewsType.Emergency => "bg-red-100 text-red-700",
        NewsType.Notice => "bg-blue-100 text-blue-700",
        NewsType.Message => "bg-slate-100 text-slate-700",
        _ => "bg-emerald-100 text-emerald-700"
    };

    private static string GetPriorityBadgeClass(NewsPriority priority) => priority switch
    {
        NewsPriority.Critical => "bg-red-100 text-red-700 font-semibold",
        NewsPriority.High => "bg-orange-100 text-orange-700",
        NewsPriority.Medium => "bg-amber-100 text-amber-700",
        _ => "bg-slate-100 text-slate-600"
    };

    public record NewsRow(
        Guid Id,
        string Title,
        string Type,
        string TypeClass,
        string Priority,
        string PriorityClass,
        string PublishedBy,
        string PublishedAt,
        bool IsRead,
        Guid PublishedById);
}
