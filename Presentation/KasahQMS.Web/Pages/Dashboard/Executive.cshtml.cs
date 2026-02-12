using System.Text.Json;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Documents.Queries;
using KasahQMS.Application.Features.Tasks.Queries;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Dashboard;

/// <summary>
/// Executive dashboard for TMD and Deputy - system-wide overview.
/// </summary>
[Authorize(Roles = "TopManagingDirector,TMD,DeputyDirector,Deputy,Deputy Country Manager,Country Manager")]
public class ExecutiveModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IMediator _mediator;
    private readonly ILogger<ExecutiveModel> _logger;

    public ExecutiveModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IMediator mediator,
        ILogger<ExecutiveModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _mediator = mediator;
        _logger = logger;
    }

    public string DisplayName { get; set; } = "Executive";
    public List<StatCard> Stats { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public List<ApprovalItem> PendingApprovals { get; set; } = new();
    public List<ActivityItem> Activity { get; set; } = new();
    public List<InsightItem> ExecutiveInsights { get; set; } = new();
    public string DocumentsTrendJson { get; set; } = "{}";
    public string CapaStatusJson { get; set; } = "{}";
    public string DepartmentBreakdownJson { get; set; } = "{}";

    public async Task OnGetAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        var tenantId = currentUser?.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (tenantId == Guid.Empty)
        {
            return;
        }

        DisplayName = currentUser?.FullName ?? "Executive";

        // Get visible user IDs (all subordinates recursively)
        var visibleUserIds = currentUser != null && _currentUserService.UserId.HasValue
            ? await _hierarchyService.GetVisibleUserIdsAsync(_currentUserService.UserId.Value)
            : new List<Guid>();

        // System-wide statistics
        var activeDocuments = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && d.Status != DocumentStatus.Archived);
        var openCapas = await _dbContext.Capas.CountAsync(c =>
            c.TenantId == tenantId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified);
        var pendingApprovals = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview));
        var upcomingAudits = await _dbContext.Audits.CountAsync(a =>
            a.TenantId == tenantId && a.PlannedStartDate <= DateTime.UtcNow.AddDays(30) &&
            a.Status != AuditStatus.Closed);

        Stats = new List<StatCard>
        {
            new("Active documents", activeDocuments.ToString(), "System-wide"),
            new("Open CAPAs", openCapas.ToString(), "Requires follow-up"),
            new("Pending approvals", pendingApprovals.ToString(), "Awaiting leadership"),
            new("Audits scheduled", upcomingAudits.ToString(), "Next 30 days")
        };

        // Get tasks using refactored query (includes hierarchy)
        var myTasksQuery = new GetMyTasksQuery { Limit = 5 };
        var tasksResult = await _mediator.Send(myTasksQuery);
        if (tasksResult.IsSuccess)
        {
            Tasks = tasksResult.Value.UpcomingTasks
                .Take(5)
                .Select(t => new TaskItem(t.Title, t.DueDate?.ToString("MMM dd") ?? "No due date", t.Status.ToString()))
                .ToList();
        }

        // Get pending approvals (documents awaiting approval)
        var approvalData = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.CurrentApproverId != null &&
                        visibleUserIds.Contains(d.CurrentApproverId.Value))
            .Join(_dbContext.Users.AsNoTracking(),
                d => d.CreatedById,
                u => u.Id,
                (d, u) => new { d.Title, u.FirstName, u.LastName, Status = d.Status.ToString() })
            .Take(5)
            .ToListAsync();

        PendingApprovals = approvalData
            .Select(a => new ApprovalItem(a.Title, $"{a.FirstName} {a.LastName}", a.Status))
            .ToList();

        // Recent activity (system-wide)
        Activity = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.Timestamp)
            .Take(6)
            .Select(a => new ActivityItem(
                a.Action.Replace("_", " "),
                a.Description ?? a.EntityType,
                a.Timestamp.ToString("MMM dd, HH:mm")))
            .ToListAsync();

        // Executive insights
        var totalDocs = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId);
        var approvedDocs = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && d.Status == DocumentStatus.Approved);

        var completedTasks = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId && t.Status == QmsTaskStatus.Completed);
        var totalTasks = await _dbContext.QmsTasks.CountAsync(t => t.TenantId == tenantId);

        var overdueItems = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId && t.Status == QmsTaskStatus.Overdue);

        ExecutiveInsights = new List<InsightItem>
        {
            new("Compliance score", totalDocs == 0 ? "0%" : $"{(int)(approvedDocs * 100.0 / totalDocs)}%", "Approved documents"),
            new("SLA adherence", totalTasks == 0 ? "0%" : $"{(int)(completedTasks * 100.0 / totalTasks)}%", "Tasks completed"),
            new("Risk hotspots", overdueItems.ToString(), "Overdue items")
        };

        DocumentsTrendJson = await BuildDocumentTrendAsync(tenantId);
        CapaStatusJson = await BuildCapaStatusAsync(tenantId);
        DepartmentBreakdownJson = await BuildDepartmentBreakdownAsync(tenantId);
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
    public record TaskItem(string Title, string DueDate, string Status);
    public record ApprovalItem(string Title, string Owner, string Stage);
    public record ActivityItem(string Title, string Description, string When);
    public record InsightItem(string Label, string Value, string Note);

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

    private async Task<string> BuildDocumentTrendAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-5), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6)
            .Select(i => start.AddMonths(i))
            .ToList();

        var grouped = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= start)
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var labels = months.Select(m => m.ToString("MMM")).ToArray();
        var values = months
            .Select(m => grouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0)
            .ToArray();

        return SerializeChart(labels, values);
    }

    private async Task<string> BuildCapaStatusAsync(Guid tenantId)
    {
        var groups = await _dbContext.Capas.AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var labels = new[] { "Draft", "Investigation", "Defined", "Implemented", "Verified", "Closed" };
        var values = new[]
        {
            groups.FirstOrDefault(g => g.Status == CapaStatus.Draft)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == CapaStatus.UnderInvestigation)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == CapaStatus.ActionsDefined)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == CapaStatus.ActionsImplemented)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == CapaStatus.EffectivenessVerified)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == CapaStatus.Closed)?.Count ?? 0
        };

        return SerializeChart(labels, values);
    }

    private async Task<string> BuildDepartmentBreakdownAsync(Guid tenantId)
    {
        var breakdown = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Join(_dbContext.Users.AsNoTracking(),
                d => d.CreatedById,
                u => u.Id,
                (d, u) => new { u.OrganizationUnitId })
            .Join(_dbContext.OrganizationUnits.AsNoTracking(),
                x => x.OrganizationUnitId,
                ou => ou.Id,
                (x, ou) => new { ou.Name })
            .GroupBy(x => x.Name)
            .Select(g => new { Department = g.Key ?? "Unassigned", Count = g.Count() })
            .ToListAsync();

        var labels = breakdown.Select(b => b.Department).ToArray();
        var values = breakdown.Select(b => b.Count).ToArray();

        return SerializeChart(labels, values);
    }
}

