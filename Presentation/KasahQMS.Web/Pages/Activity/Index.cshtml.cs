using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Security;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Activity;

[Microsoft.AspNetCore.Authorization.Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly KasahQMS.Application.Common.Security.IAuthorizationService _authorizationService;

    private const int PageSize = 50;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        KasahQMS.Application.Common.Security.IAuthorizationService authorizationService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
    }

    [BindProperty(SupportsGet = true)] public string? UserFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? EntityTypeFilter { get; set; }
    [BindProperty(SupportsGet = true)] public string? ActionFilter { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;

    public List<ActivityEntry> Entries { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public List<string> AvailableEntityTypes { get; set; } = new();
    public List<string> AvailableActions { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await _authorizationService.HasPermissionAsync(Permissions.AuditLogs.View))
            return RedirectToPage("/Account/AccessDenied");

        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Load filter options
        AvailableEntityTypes = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.EntityType).Distinct().OrderBy(e => e).ToListAsync();
        AvailableActions = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync();

        var query = _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrEmpty(UserFilter))
        {
            if (Guid.TryParse(UserFilter, out var uid))
                query = query.Where(a => a.UserId == uid);
        }
        if (!string.IsNullOrEmpty(EntityTypeFilter))
            query = query.Where(a => a.EntityType == EntityTypeFilter);
        if (!string.IsNullOrEmpty(ActionFilter))
            query = query.Where(a => a.Action == ActionFilter);

        TotalCount = await query.CountAsync();
        if (PageNumber < 1) PageNumber = 1;

        var entries = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Include(a => a.User)
            .ToListAsync();

        Entries = entries.Select(a => new ActivityEntry(
            a.Id,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.Description ?? $"{a.Action} on {a.EntityType}",
            a.User?.FullName ?? "System",
            a.User?.FullName != null ? GetInitials(a.User.FullName) : "SY",
            a.Timestamp,
            FormatRelativeTime(a.Timestamp),
            a.IsSuccessful,
            GetActionIcon(a.Action)
        )).ToList();

        return Page();
    }

    private static string GetInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpper()
            : name.Length >= 2 ? name[..2].ToUpper() : name.ToUpper();
    }

    private static string FormatRelativeTime(DateTime timestamp)
    {
        var diff = DateTime.UtcNow - timestamp;
        if (diff.TotalMinutes < 1) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return timestamp.ToString("MMM dd, yyyy HH:mm");
    }

    private static string GetActionIcon(string action)
    {
        return action.ToLower() switch
        {
            var a when a.Contains("create") => "plus-circle",
            var a when a.Contains("update") || a.Contains("edit") => "pencil",
            var a when a.Contains("delete") => "trash",
            var a when a.Contains("approve") => "check-circle",
            var a when a.Contains("reject") => "x-circle",
            var a when a.Contains("login") || a.Contains("auth") => "login",
            _ => "activity"
        };
    }

    public record ActivityEntry(
        Guid Id, string Action, string EntityType, Guid? EntityId,
        string Description, string UserName, string UserInitials,
        DateTime Timestamp, string RelativeTime, bool IsSuccessful, string Icon);
}
