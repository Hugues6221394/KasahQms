using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
    }

    public bool RequireMfa { get; set; }
    public bool StrongPasswords { get; set; }
    public int LockoutThreshold { get; set; }
    public int CapaEscalationDays { get; set; }
    public string AuditReminderCadence { get; set; } = "Weekly";
    public int AuditLogRetentionYears { get; set; }
    public int DocumentArchiveMonths { get; set; }
    public string BackupFrequency { get; set; } = "Daily";
    [Microsoft.AspNetCore.Mvc.BindProperty] public bool MaintenanceModeEnabled { get; set; }
    [Microsoft.AspNetCore.Mvc.BindProperty] public string? MaintenanceModeMessage { get; set; }
    public bool CanManageSystemSettings { get; set; }
    public string? ActionMessage { get; set; }

    public async Task OnGetAsync()
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId.HasValue)
        {
            var currentUser = await _dbContext.Users.AsNoTracking()
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == currentUserId.Value);
            CanManageSystemSettings = currentUser?.Roles?.Any(r =>
                r.Name is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin") == true;
        }

        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var settings = await _dbContext.SystemSettings.AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        RequireMfa = GetBool(settings, "Security.RequireMfa");
        StrongPasswords = GetBool(settings, "Security.StrongPasswords");
        LockoutThreshold = GetInt(settings, "Security.LockoutThreshold", 5);
        CapaEscalationDays = GetInt(settings, "Notifications.CapaEscalationDays", 7);
        AuditReminderCadence = GetString(settings, "Notifications.AuditReminderCadence", "Weekly");
        AuditLogRetentionYears = GetInt(settings, "Retention.AuditLogYears", 7);
        DocumentArchiveMonths = GetInt(settings, "Retention.DocumentArchiveMonths", 24);
        BackupFrequency = GetString(settings, "Backups.Frequency", "Daily");
        MaintenanceModeEnabled = GetBool(settings, "System.MaintenanceMode.Enabled");
        MaintenanceModeMessage = GetString(settings, "System.MaintenanceMode.Message", "System is temporarily under maintenance. Please try again shortly.");
    }

    public async Task<Microsoft.AspNetCore.Mvc.IActionResult> OnPostMaintenanceAsync()
    {
        var currentUserId = _currentUserService.UserId;
        if (!currentUserId.HasValue)
            return RedirectToPage("/Account/Login");

        var currentUser = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId.Value);
        var isSystemAdmin = currentUser?.Roles?.Any(r =>
            r.Name is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin") == true;
        if (!isSystemAdmin)
            return RedirectToPage("/Account/AccessDenied");

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        if (tenantId == Guid.Empty) return RedirectToPage();

        var message = string.IsNullOrWhiteSpace(MaintenanceModeMessage)
            ? "System is temporarily under maintenance. Please try again shortly."
            : MaintenanceModeMessage.Trim();

        await UpsertSettingAsync(tenantId, currentUserId.Value, "System.MaintenanceMode.Enabled", MaintenanceModeEnabled.ToString());
        await UpsertSettingAsync(tenantId, currentUserId.Value, "System.MaintenanceMode.Message", message);
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            MaintenanceModeEnabled ? "SYSTEM_MAINTENANCE_ENABLED" : "SYSTEM_MAINTENANCE_DISABLED",
            "SystemSettings",
            tenantId,
            MaintenanceModeEnabled ? $"Maintenance mode enabled. Message: {message}" : "Maintenance mode disabled",
            CancellationToken.None);

        return RedirectToPage(new { saved = true });
    }

    private static bool GetBool(Dictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) && parsed;
    }

    private static int GetInt(Dictionary<string, string> settings, string key, int fallback)
    {
        return settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static string GetString(Dictionary<string, string> settings, string key, string fallback)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private async Task UpsertSettingAsync(Guid tenantId, Guid userId, string key, string value)
    {
        var setting = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == key);
        if (setting == null)
        {
            setting = KasahQMS.Domain.Entities.Configuration.SystemSetting.Create(tenantId, key, value, userId);
            _dbContext.SystemSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.LastModifiedAt = DateTime.UtcNow;
            setting.LastModifiedById = userId;
        }
    }
}

