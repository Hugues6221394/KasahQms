using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Supervision;

[Authorize(Roles = "TMD, Deputy, TopManagingDirector, DeputyDirector, Country Manager, Deputy Country Manager, SystemAdmin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<ActivityLogViewModel> RecentActivities { get; set; } = new();
    public List<LoginHourViewModel> LoginByHour { get; set; } = new();
    public int ActiveUsersCount { get; set; }

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId;
        if (tenantId == null) return;

        // 1. Get cross-departmental activities (Recent Audit Logs)
        RecentActivities = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new ActivityLogViewModel
            {
                Timestamp = a.Timestamp.ToString("MMM dd, HH:mm"),
                UserId = a.UserId,
                UserName = "User", // We'll join or lookup if needed
                Action = a.Action,
                Entity = a.EntityType,
                Description = a.Description
            })
            .ToListAsync();

        // Populate Usernames (manual join for simplicity/optimization in this POC)
        var userIds = RecentActivities.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();
        var userMap = await _dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        foreach (var act in RecentActivities)
        {
            if (act.UserId.HasValue && userMap.TryGetValue(act.UserId.Value, out var name))
            {
                act.UserName = name;
            }
        }

        // 2. Aggregate Logins by Hour (Last 24 hours)
        var last24h = DateTime.UtcNow.AddHours(-24);
        var logins = await _dbContext.UserLoginActivities
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.Timestamp >= last24h && a.EventType == "Login")
            .OrderBy(a => a.Timestamp)
            .ToListAsync();

        LoginByHour = logins
            .GroupBy(l => l.Timestamp.Hour)
            .Select(g => new LoginHourViewModel
            {
                Hour = g.Key,
                Count = g.Count()
            })
            .OrderBy(h => h.Hour)
            .ToList();

        // 3. Count active users (logged in within last 1 hour)
        var lastHour = DateTime.UtcNow.AddHours(-1);
        ActiveUsersCount = await _dbContext.UserLoginActivities
            .Where(a => a.TenantId == tenantId && a.Timestamp >= lastHour && a.EventType == "Login")
            .Select(a => a.UserId)
            .Distinct()
            .CountAsync();
    }

    public class ActivityLogViewModel
    {
        public string Timestamp { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class LoginHourViewModel
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }
}
