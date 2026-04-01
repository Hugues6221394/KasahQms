using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AppAuthService = KasahQMS.Application.Common.Security.IAuthorizationService;

namespace KasahQMS.Web.Pages.News;

[Microsoft.AspNetCore.Authorization.Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly AppAuthService _authorizationService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        INotificationService notificationService,
        AppAuthService authorizationService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _notificationService = notificationService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ArticleContent { get; set; } = string.Empty;
    [BindProperty] public string Type { get; set; } = "Message";
    [BindProperty] public string Priority { get; set; } = "Medium";

    public async Task<IActionResult> OnGetAsync()
    {
        // Check permission
        if (!await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Create))
            return RedirectToPage("/Account/AccessDenied");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Check permission
        if (!await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.News.Create))
            return RedirectToPage("/Account/AccessDenied");

        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Title is required.");

        if (string.IsNullOrWhiteSpace(ArticleContent))
            ModelState.AddModelError(nameof(ArticleContent), "Content is required.");

        if (!ModelState.IsValid)
            return Page();

        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId 
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        if (!Enum.TryParse<NewsType>(Type, out var newsType))
            newsType = NewsType.Message;

        if (!Enum.TryParse<NewsPriority>(Priority, out var newsPriority))
            newsPriority = NewsPriority.Medium;

        var article = new Domain.Entities.News.NewsArticle
        {
            Id = Guid.NewGuid(),
            Title = Title,
            Content = ArticleContent,
            Type = newsType,
            Priority = newsPriority,
            PublishedAt = DateTime.UtcNow,
            PublishedById = userId.Value,
            IsActive = true,
            TenantId = tenantId,
            CreatedById = userId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.NewsArticles.Add(article);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("News article {Id} published by {UserId}: {Title}", article.Id, userId, Title);

        await SendNotificationsAsync(article, tenantId, userId.Value);

        return RedirectToPage("./Index");
    }

    private async Task SendNotificationsAsync(Domain.Entities.News.NewsArticle article, Guid tenantId, Guid publisherId)
    {
        try
        {
            var users = await _dbContext.Users
                .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != publisherId)
                .Select(u => new { u.Id, u.Email, u.FirstName, u.LastName })
                .ToListAsync();

            foreach (var user in users)
            {
                try
                {
                    // Send in-app notification
                    await _notificationService.SendAsync(
                        user.Id,
                        $"New {article.Type}: {article.Title}",
                        $"A new {article.Type.ToString().ToLower()} has been posted. Priority: {article.Priority}",
                        NotificationType.System,
                        article.Id);

                    // Send email notification
                    var subject = $"[{article.Type}] {article.Title}";
                    var body = $@"
<h2>{article.Title}</h2>
<p><strong>Type:</strong> {article.Type} | <strong>Priority:</strong> {article.Priority}</p>
<p><strong>Published:</strong> {article.PublishedAt:MMMM dd, yyyy HH:mm}</p>
<hr/>
<div>{article.Content}</div>
<hr/>
<p><a href='{GetBaseUrl()}/News/Details?id={article.Id}'>Read Full Article</a></p>
";

                    if (!string.IsNullOrWhiteSpace(user.Email))
                    {
                        await _emailService.SendEmailAsync(user.Email, subject, body);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send news notification to {Email}", user.Email);
                }
            }

            _logger.LogInformation("News notifications sent for article {Id}", article.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send news notifications for article {Id}", article.Id);
        }
    }

    private string GetBaseUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }
}
