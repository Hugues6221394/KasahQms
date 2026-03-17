using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AppAuthService = KasahQMS.Application.Common.Security.IAuthorizationService;

namespace KasahQMS.Web.Pages.News;

[Microsoft.AspNetCore.Authorization.Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly AppAuthService _authorizationService;

    public EditModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        AppAuthService authorizationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
    }

    [BindProperty] public Guid Id { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ArticleContent { get; set; } = string.Empty;
    [BindProperty] public string Type { get; set; } = "Message";
    [BindProperty] public string Priority { get; set; } = "Medium";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == default)
            return RedirectToPage("/Account/Login");

        var article = await _dbContext.NewsArticles
            .FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId && n.IsActive);

        if (article == null)
            return NotFound();

        // Check permission: creator or user with News.Edit
        var canEdit = await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Edit);
        if (!canEdit && article.PublishedById != userId)
            return RedirectToPage("/Account/AccessDenied");

        Id = article.Id;
        Title = article.Title;
        ArticleContent = article.Content;
        Type = article.Type.ToString();
        Priority = article.Priority.ToString();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == default)
            return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Title is required.");

        if (string.IsNullOrWhiteSpace(ArticleContent))
            ModelState.AddModelError(nameof(ArticleContent), "Content is required.");

        if (!ModelState.IsValid)
            return Page();

        var article = await _dbContext.NewsArticles
            .FirstOrDefaultAsync(n => n.Id == Id && n.TenantId == tenantId && n.IsActive);

        if (article == null)
            return NotFound();

        // Check permission
        var canEdit = await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Edit);
        if (!canEdit && article.PublishedById != userId)
            return RedirectToPage("/Account/AccessDenied");

        if (!Enum.TryParse<NewsType>(Type, out var newsType))
            newsType = NewsType.Message;

        if (!Enum.TryParse<NewsPriority>(Priority, out var newsPriority))
            newsPriority = NewsPriority.Medium;

        article.Title = Title;
        article.Content = ArticleContent;
        article.Type = newsType;
        article.Priority = newsPriority;
        article.LastModifiedById = userId;
        article.LastModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return RedirectToPage("./Index");
    }
}
