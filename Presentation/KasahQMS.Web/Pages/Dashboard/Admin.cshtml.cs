using System.Text.Json;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Dashboard;

/// <summary>
/// Admin dashboard for System Admin - full system management view.
/// </summary>
[Authorize(Roles = "SystemAdmin,System Admin,SystemAdministrator,TenantAdmin")]
public class AdminModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AdminModel> _logger;

    public AdminModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<AdminModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public string DisplayName { get; set; } = "Admin";
    public List<StatCard> Stats { get; set; } = new();
    public List<UserItem> RecentUsers { get; set; } = new();
    public List<ActivityItem> Activity { get; set; } = new();
    public List<LatestNewsItem> LatestNews { get; set; } = new();
    public string UsersTrendJson { get; set; } = "{}";
    public string SystemHealthJson { get; set; } = "{}";
    public int TotalUsersCount { get; set; }
    public int ActiveDocumentsCount { get; set; }
    public int OpenTasksCount { get; set; }
    public int ActiveUsersCount { get; set; }
    public double UserGrowthPercent { get; set; }
    public string CurrentDate { get; set; } = "";

    public async Task OnGetAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        var tenantId = currentUser?.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (tenantId == Guid.Empty)
        {
            return;
        }

        DisplayName = currentUser?.FullName ?? "Admin";

        // System-wide statistics
        var totalUsers = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId);
        var activeUsers = await _dbContext.Users.CountAsync(u => 
            u.TenantId == tenantId && 
            u.IsActive);
        var totalDocuments = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId);
        var totalAuditLogs = await _dbContext.AuditLogEntries.CountAsync(a => a.TenantId == tenantId);
        var openTasks = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId &&
            t.Status != QmsTaskStatus.Completed &&
            t.Status != QmsTaskStatus.Cancelled);

        TotalUsersCount = totalUsers;
        ActiveDocumentsCount = totalDocuments;
        OpenTasksCount = openTasks;
        ActiveUsersCount = activeUsers;
        CurrentDate = DateTime.UtcNow.ToString("dddd, MMMM dd, yyyy");

        var thisMonth = DateTime.SpecifyKind(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1), DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);
        var lastMonthCount = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId && u.CreatedAt >= lastMonth && u.CreatedAt < thisMonth);
        var thisMonthCount = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId && u.CreatedAt >= thisMonth);
        UserGrowthPercent = lastMonthCount > 0 ? Math.Round((thisMonthCount - lastMonthCount) * 100.0 / lastMonthCount, 1) : 0;

        Stats = new List<StatCard>
        {
            new("Total Users", totalUsers.ToString(), $"{(UserGrowthPercent >= 0 ? "+" : "")}{UserGrowthPercent}% this month"),
            new("Active Documents", totalDocuments.ToString(), "System-wide"),
            new("Open Tasks", openTasks.ToString(), "In progress"),
            new("System Health", "98%", $"{activeUsers} active users")
        };

        // Recent users
        RecentUsers = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .Select(u => new UserItem(u.FullName, u.Email ?? "N/A", u.IsActive ? "Active" : "Inactive", u.CreatedAt.ToString("MMM dd, yyyy")))
            .ToListAsync();

        // Recent activity (system-wide)
        Activity = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .Select(a => new ActivityItem(
                a.Action.Replace("_", " "),
                a.Description ?? a.EntityType,
                a.Timestamp.ToString("MMM dd, HH:mm")))
            .ToListAsync();

        UsersTrendJson = await BuildUsersTrendAsync(tenantId);
        SystemHealthJson = await BuildSystemHealthAsync(tenantId);
        LatestNews = await _dbContext.NewsArticles.AsNoTracking()
            .Where(n => n.TenantId == tenantId && n.IsActive)
            .OrderByDescending(n => n.PublishedAt)
            .Take(5)
            .Select(n => new LatestNewsItem(
                n.Id,
                n.Title,
                n.Content.Length > 160 ? n.Content.Substring(0, 160) + "..." : n.Content,
                n.PublishedAt.ToString("MMM dd, yyyy")))
            .ToListAsync();
    }

    private static string SerializeChart(IEnumerable<string> labels, IEnumerable<int> values)
    {
        return JsonSerializer.Serialize(new
        {
            labels,
            values
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public record StatCard(string Title, string Value, string Subtitle);
    public record UserItem(string Name, string Email, string Status, string Created);
    public record ActivityItem(string Title, string Description, string When);
    public record LatestNewsItem(Guid Id, string Title, string Summary, string PublishedAt);

    private async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        }

        return null;
    }

    private async Task<string> BuildUsersTrendAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-5), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6)
            .Select(i => start.AddMonths(i))
            .ToList();

        var grouped = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.CreatedAt >= start)
            .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var labels = months.Select(m => m.ToString("MMM")).ToArray();
        var values = months
            .Select(m => grouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0)
            .ToArray();

        return SerializeChart(labels, values);
    }

    private async Task<string> BuildSystemHealthAsync(Guid tenantId)
    {
        var activeUsers = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive);
        var inactiveUsers = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId && !u.IsActive);
        var approvedDocs = await _dbContext.Documents.CountAsync(d => 
            d.TenantId == tenantId && d.Status == DocumentStatus.Approved);
        var pendingDocs = await _dbContext.Documents.CountAsync(d => 
            d.TenantId == tenantId && (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview));

        var labels = new[] { "Active Users", "Inactive Users", "Approved Docs", "Pending Approval Docs" };
        var values = new[]
        {
            activeUsers,
            inactiveUsers,
            approvedDocs,
            pendingDocs
        };

        return SerializeChart(labels, values);
    }
}

