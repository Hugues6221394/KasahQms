using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.News;

[Microsoft.AspNetCore.Authorization.Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DetailsModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public NewsDetailView? Article { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId 
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        var article = await _dbContext.NewsArticles.AsNoTracking()
            .Include(n => n.PublishedBy)
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);

        if (article == null)
            return NotFound();

        Article = new NewsDetailView(
            article.Id,
            article.Title,
            article.Content,
            article.Type.ToString(),
            GetTypeBadgeClass(article.Type),
            article.Priority.ToString(),
            GetPriorityBadgeClass(article.Priority),
            article.PublishedBy != null ? article.PublishedBy.FirstName + " " + article.PublishedBy.LastName : "—",
            article.PublishedAt.ToString("MMMM dd, yyyy HH:mm")
        );

        // Mark as read if not already
        var alreadyRead = await _dbContext.NewsReads
            .AnyAsync(nr => nr.NewsArticleId == id && nr.UserId == userId);

        if (!alreadyRead)
        {
            _dbContext.NewsReads.Add(new Domain.Entities.News.NewsRead
            {
                Id = Guid.NewGuid(),
                NewsArticleId = id,
                UserId = userId.Value,
                ReadAt = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync();
        }

        return Page();
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

    public record NewsDetailView(
        Guid Id,
        string Title,
        string Content,
        string Type,
        string TypeClass,
        string Priority,
        string PriorityClass,
        string PublishedBy,
        string PublishedAt);
}
