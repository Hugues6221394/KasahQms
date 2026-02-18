using System.Text.Json;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Analytics;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHierarchyService _hierarchyService;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        IHierarchyService hierarchyService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _hierarchyService = hierarchyService;
    }

    public List<KpiItem> Kpis { get; set; } = new();
    public List<DepartmentMetric> DepartmentMetrics { get; set; } = new();
    public string ComplianceTrendJson { get; set; } = "{}";
    public string FindingSeverityJson { get; set; } = "{}";

    public bool IsScopedToDepartment { get; set; }
    public string? ScopedDepartmentName { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return RedirectToPage("/Account/Login");

        // Check view permission
        if (!await _authorizationService.HasPermissionAsync(Permissions.Analytics.View))
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        // Determine scope
        // TMD, Deputy, Auditors, Admin see all
        var canViewAll = await _authorizationService.HasAnyPermissionAsync(new[]
        {
            Permissions.Users.ViewAll,
            Permissions.System.ViewSystemHealth,
            Permissions.AuditLogs.View
        });

        var user = await _dbContext.Users.AsNoTracking()
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        Guid? scopedOrgUnitId = null;

        if (!canViewAll)
        {
            // Department Manager / Staff see their department only
            scopedOrgUnitId = user?.OrganizationUnitId;
            IsScopedToDepartment = true;
            ScopedDepartmentName = user?.OrganizationUnit?.Name;
        }

        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Calculate KPIs
        await CalculateKpis(tenantId, scopedOrgUnitId);

        // Calculate Department Metrics (filtered if scoped)
        DepartmentMetrics = await BuildDepartmentMetricsAsync(tenantId, scopedOrgUnitId);

        // Build Charts
        ComplianceTrendJson = await BuildComplianceTrendAsync(tenantId, scopedOrgUnitId);
        FindingSeverityJson = await BuildFindingSeverityAsync(tenantId, scopedOrgUnitId);

        return Page();
    }

    private async Task CalculateKpis(Guid tenantId, Guid? orgUnitId)
    {
        var docsQuery = _dbContext.Documents.AsNoTracking().Where(d => d.TenantId == tenantId);
        var tasksQuery = _dbContext.QmsTasks.AsNoTracking().Where(t => t.TenantId == tenantId);
        var capasQuery = _dbContext.Capas.AsNoTracking().Where(c => c.TenantId == tenantId);

        // Scope queries if org unit is set
        if (orgUnitId.HasValue)
        {
            // Get all user IDs in this org unit
            var unitUserIds = await _dbContext.Users
                .Where(u => u.OrganizationUnitId == orgUnitId.Value)
                .Select(u => u.Id)
                .ToListAsync();

            docsQuery = docsQuery.Where(d => unitUserIds.Contains(d.CreatedById));
            // For tasks, check if assigned to user in unit OR assigned to unit directly
            tasksQuery = tasksQuery.Where(t =>
                (t.AssignedToId.HasValue && unitUserIds.Contains(t.AssignedToId.Value)) ||
                t.AssignedToOrgUnitId == orgUnitId.Value);
            capasQuery = capasQuery.Where(c =>
                c.OwnerId.HasValue && unitUserIds.Contains(c.OwnerId.Value));
        }

        var totalDocs = await docsQuery.CountAsync();
        var approvedDocs = await docsQuery.CountAsync(d => d.Status == DocumentStatus.Approved);

        // Audits are usually tenant-wide, but we could try to filter by lead auditor's dept if needed
        // For now, keep audits global or simple if scoped (Audits might not be department specific easily)
        // If scoped, maybe show only audits where auditee is in department? (Not modeled yet)
        // Let's assume Audit visibility is broader for now, or just show 0 if not applicable.
        // Simplified: Audit KPIs might be irrelevant for simple Dept view, or read-only global.
        var totalAudits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId);
        var completedAudits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && a.Status == AuditStatus.Completed);

        var closedCapas = await capasQuery
            .Where(c => (c.Status == CapaStatus.Closed || c.Status == CapaStatus.EffectivenessVerified) && c.ActualCompletionDate.HasValue)
            .Select(c => new { c.CreatedAt, c.ActualCompletionDate })
            .ToListAsync();

        var trainingTasks = await tasksQuery.CountAsync(t => t.Title.ToLower().Contains("training"));
        var completedTraining = await tasksQuery.CountAsync(t => t.Title.ToLower().Contains("training") && t.Status == QmsTaskStatus.Completed);

        Kpis = new List<KpiItem>
        {
            new("Document Compliance", totalDocs == 0 ? "N/A" : $"{(int)(approvedDocs * 100.0 / totalDocs)}%", "Approved vs Total"),
            new("Audit Status", totalAudits == 0 ? "N/A" : $"{(int)(completedAudits * 100.0 / totalAudits)}%", "Completion Rate"),
            new("Avg CAPA Closure", closedCapas.Count == 0 ? "N/A" : $"{(int)closedCapas.Average(c => (c.ActualCompletionDate!.Value - c.CreatedAt).TotalDays)} days", "Days to Close"),
            new("Training Completion", trainingTasks == 0 ? "N/A" : $"{(int)(completedTraining * 100.0 / trainingTasks)}%", "Assigned Tasks")
        };
    }

    private static string SerializeChart(IEnumerable<string> labels, IEnumerable<int> values)
    {
        return JsonSerializer.Serialize(new
        {
            labels,
            values
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public record KpiItem(string Label, string Value, string Note);
    public record DepartmentMetric(string Department, string Compliance, string OpenCapa, string Readiness);

    private async Task<List<DepartmentMetric>> BuildDepartmentMetricsAsync(Guid tenantId, Guid? filterOrgUnitId)
    {
        var orgUnitsQuery = _dbContext.OrganizationUnits.AsNoTracking().Where(o => o.TenantId == tenantId);

        if (filterOrgUnitId.HasValue)
        {
            orgUnitsQuery = orgUnitsQuery.Where(o => o.Id == filterOrgUnitId.Value);
        }

        var orgUnits = await orgUnitsQuery.ToListAsync();

        // Pre-fetch data for in-memory aggregation (optimized for moderate dataset)
        // For larger datasets, use direct SQL grouping.

        var users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => new { u.Id, u.OrganizationUnitId })
            .ToListAsync();

        var docs = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new { d.CreatedById, d.Status })
            .ToListAsync();

        var capas = await _dbContext.Capas.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified)
            .Select(c => c.OwnerId)
            .ToListAsync();

        var metrics = new List<DepartmentMetric>();

        foreach (var unit in orgUnits)
        {
            var unitUserIds = users.Where(u => u.OrganizationUnitId == unit.Id).Select(u => u.Id).ToList();
            var unitDocs = docs.Where(d => unitUserIds.Contains(d.CreatedById)).ToList();
            var unitApproved = unitDocs.Count(d => d.Status == DocumentStatus.Approved);
            var compliance = unitDocs.Count == 0 ? 0 : (int)(unitApproved * 100.0 / unitDocs.Count);
            var openCapaCount = capas.Count(c => c.HasValue && unitUserIds.Contains(c.Value));

            var readiness = compliance >= 90 && openCapaCount <= 2 ? "High" :
                compliance >= 75 ? "Medium" : "Low";

            metrics.Add(new DepartmentMetric(
                unit.Name,
                $"{compliance}%",
                openCapaCount.ToString(),
                readiness));
        }

        return metrics;
    }

    private async Task<string> BuildComplianceTrendAsync(Guid tenantId, Guid? orgUnitId)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-5), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6).Select(i => start.AddMonths(i)).ToList();

        var query = _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= start);

        if (orgUnitId.HasValue)
        {
             var unitUserIds = await _dbContext.Users
                .Where(u => u.OrganizationUnitId == orgUnitId.Value)
                .Select(u => u.Id)
                .ToListAsync();
             query = query.Where(d => unitUserIds.Contains(d.CreatedById));
        }

        var grouped = await query
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var labels = months.Select(m => m.ToString("MMM")).ToArray();
        var values = months
            .Select(m => grouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0)
            .ToArray();

        return SerializeChart(labels, values);
    }

    private async Task<string> BuildFindingSeverityAsync(Guid tenantId, Guid? orgUnitId)
    {
        // Audit Findings are linked to Audits.
        // If scoped to Department, logic depends on if Audit has a 'DepartmentId' or similar.
        // The Audit entity doesn't seem to have DepartmentId directly (checked in other files).
        // It might be linked via LeadAuditor or Scope.
        // For now, if scoped, we might return empty or global. Let's return global but maybe filtered by assignee if possible.
        // Since Audit findings aren't explicitly owned by a department in the provided schema, we'll keep this global
        // OR filtering by "if the audit lead auditor is in the department".

        var query = _dbContext.AuditFindings.AsNoTracking()
            .Join(_dbContext.Audits.AsNoTracking().Where(a => a.TenantId == tenantId),
                f => f.AuditId,
                a => a.Id,
                (f, a) => new { Finding = f, Audit = a });

        // Apply filter logic if needed/possible.
        // Assuming global view for findings is acceptable or "view all" for now as schema limitations exist.
        // If strict scoping is needed, we'd need to extend Audit entity.

        var grouped = await query
            .GroupBy(x => x.Finding.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync();

        var labels = new[] { "Minor", "Major", "Critical", "Observation" };
        var values = new[]
        {
            grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Minor)?.Count ?? 0,
            grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Major)?.Count ?? 0,
            grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Critical)?.Count ?? 0,
            grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Observation)?.Count ?? 0
        };

        return SerializeChart(labels, values);
    }
}
