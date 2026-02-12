using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Settings;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public bool RequireMfa { get; set; }
    public bool StrongPasswords { get; set; }
    public int LockoutThreshold { get; set; }
    public int CapaEscalationDays { get; set; }
    public string AuditReminderCadence { get; set; } = "Weekly";
    public int AuditLogRetentionYears { get; set; }
    public int DocumentArchiveMonths { get; set; }
    public string BackupFrequency { get; set; } = "Daily";

    public async Task OnGetAsync()
    {
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
}

