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

    // Notification preferences
    [BindProperty] public bool PrefDocumentApprovals { get; set; } = true;
    [BindProperty] public bool PrefTaskAssignments { get; set; } = true;
    [BindProperty] public bool PrefCapaUpdates { get; set; } = true;
    [BindProperty] public bool PrefAuditSchedules { get; set; } = true;
    [BindProperty] public bool PrefSystemAlerts { get; set; } = true;
    public bool PreferencesSaved { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null) return;

        Notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt)
            .Take(25)
            .Select(n => new NotificationItem(
                n.Id,
                n.Title,
                n.Message,
                n.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                n.CreatedAt,
                n.IsRead,
                n.Type.ToString(),
                n.RelatedEntityId,
                n.RelatedEntityType
            ))
            .ToListAsync();

        await LoadPreferencesAsync(userId.Value, tenantId.Value);
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

    public async Task<IActionResult> OnPostSavePreferencesAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null) return Unauthorized();

        var prefMap = new Dictionary<string, bool>
        {
            [$"notify.{userId}.document_approvals"] = PrefDocumentApprovals,
            [$"notify.{userId}.task_assignments"] = PrefTaskAssignments,
            [$"notify.{userId}.capa_updates"] = PrefCapaUpdates,
            [$"notify.{userId}.audit_schedules"] = PrefAuditSchedules,
            [$"notify.{userId}.system_alerts"] = PrefSystemAlerts
        };

        foreach (var (key, value) in prefMap)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value && s.Key == key);

            if (setting != null)
            {
                setting.Value = value.ToString();
            }
            else
            {
                var newSetting = Domain.Entities.Configuration.SystemSetting.Create(
                    tenantId.Value, key, value.ToString(), userId.Value,
                    "Email notification preference");
                _dbContext.SystemSettings.Add(newSetting);
            }
        }

        await _dbContext.SaveChangesAsync();
        PreferencesSaved = true;

        // Reload page data
        Notifications = await _dbContext.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.CreatedAt).Take(25)
            .Select(n => new NotificationItem(n.Id, n.Title, n.Message,
                n.CreatedAt.ToString("MMM dd, yyyy HH:mm"), n.CreatedAt, n.IsRead,
                n.Type.ToString(), n.RelatedEntityId, n.RelatedEntityType))
            .ToListAsync();

        return Page();
    }

    private async Task LoadPreferencesAsync(Guid userId, Guid tenantId)
    {
        var keys = new[]
        {
            $"notify.{userId}.document_approvals",
            $"notify.{userId}.task_assignments",
            $"notify.{userId}.capa_updates",
            $"notify.{userId}.audit_schedules",
            $"notify.{userId}.system_alerts"
        };

        var settings = await _dbContext.SystemSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId && keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        PrefDocumentApprovals = GetPref(settings, $"notify.{userId}.document_approvals");
        PrefTaskAssignments = GetPref(settings, $"notify.{userId}.task_assignments");
        PrefCapaUpdates = GetPref(settings, $"notify.{userId}.capa_updates");
        PrefAuditSchedules = GetPref(settings, $"notify.{userId}.audit_schedules");
        PrefSystemAlerts = GetPref(settings, $"notify.{userId}.system_alerts");
    }

    private static bool GetPref(Dictionary<string, string> settings, string key)
    {
        return !settings.TryGetValue(key, out var val) || !bool.TryParse(val, out var b) || b;
    }

    public record NotificationItem(
        Guid Id, 
        string Title, 
        string Message, 
        string Time, 
        DateTime CreatedAt,
        bool IsRead, 
        string Type, 
        Guid? RelatedEntityId, 
        string? RelatedEntityType);
}
