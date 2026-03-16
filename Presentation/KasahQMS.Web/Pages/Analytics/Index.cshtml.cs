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

    // KPI properties
    public int TotalDocuments { get; set; }
    public int ApprovedRate { get; set; }
    public int OpenCapas { get; set; }
    public int AvgCapaResolutionDays { get; set; }
    public int TaskCompletionRate { get; set; }
    public int AuditCoveragePercent { get; set; }
    public int AuditFindingsCount { get; set; }
    public double AvgRiskScore { get; set; }
    public int TrainingComplianceRate { get; set; }

    // Chart data
    public string DocStatusChartJson { get; set; } = "{}";
    public string MonthlyTrendJson { get; set; } = "{}";
    public string CapaAgingJson { get; set; } = "{}";
    public string RiskHeatMapJson { get; set; } = "{}";
    public string SupplierPerformanceJson { get; set; } = "{}";
    public string FindingSeverityJson { get; set; } = "{}";

    // Department metrics
    public List<DepartmentMetric> DepartmentMetrics { get; set; } = new();
    public bool IsScopedToDepartment { get; set; }
    public string? ScopedDepartmentName { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return RedirectToPage("/Account/Login");

        if (!await _authorizationService.HasPermissionAsync(Permissions.Analytics.View))
            return RedirectToPage("/Account/AccessDenied");

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
            scopedOrgUnitId = user?.OrganizationUnitId;
            IsScopedToDepartment = true;
            ScopedDepartmentName = user?.OrganizationUnit?.Name;
        }

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        List<Guid>? unitUserIds = null;
        if (scopedOrgUnitId.HasValue)
        {
            unitUserIds = await _dbContext.Users
                .Where(u => u.OrganizationUnitId == scopedOrgUnitId.Value)
                .Select(u => u.Id).ToListAsync();
        }

        await CalculateKpis(tenantId, unitUserIds, scopedOrgUnitId);
        await BuildAllCharts(tenantId, unitUserIds, scopedOrgUnitId);
        DepartmentMetrics = await BuildDepartmentMetricsAsync(tenantId, scopedOrgUnitId);

        return Page();
    }

    private async Task CalculateKpis(Guid tenantId, List<Guid>? unitUserIds, Guid? orgUnitId)
    {
        var docsQuery = _dbContext.Documents.AsNoTracking().Where(d => d.TenantId == tenantId);
        var tasksQuery = _dbContext.QmsTasks.AsNoTracking().Where(t => t.TenantId == tenantId);
        var capasQuery = _dbContext.Capas.AsNoTracking().Where(c => c.TenantId == tenantId);

        if (unitUserIds != null)
        {
            docsQuery = docsQuery.Where(d => unitUserIds.Contains(d.CreatedById));
            tasksQuery = tasksQuery.Where(t =>
                (t.AssignedToId.HasValue && unitUserIds.Contains(t.AssignedToId.Value)) ||
                t.AssignedToOrgUnitId == orgUnitId!.Value);
            capasQuery = capasQuery.Where(c => c.OwnerId.HasValue && unitUserIds.Contains(c.OwnerId.Value));
        }

        TotalDocuments = await docsQuery.CountAsync();
        var approvedDocs = await docsQuery.CountAsync(d => d.Status == DocumentStatus.Approved);
        ApprovedRate = TotalDocuments == 0 ? 0 : (int)(approvedDocs * 100.0 / TotalDocuments);

        OpenCapas = await capasQuery.CountAsync(c =>
            c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified);

        var closedCapas = await capasQuery
            .Where(c => (c.Status == CapaStatus.Closed || c.Status == CapaStatus.EffectivenessVerified) && c.ActualCompletionDate.HasValue)
            .Select(c => new { c.CreatedAt, c.ActualCompletionDate }).ToListAsync();
        AvgCapaResolutionDays = closedCapas.Count == 0 ? 0 :
            (int)closedCapas.Average(c => (c.ActualCompletionDate!.Value - c.CreatedAt).TotalDays);

        var totalTasks = await tasksQuery.CountAsync();
        var completedTasks = await tasksQuery.CountAsync(t => t.Status == QmsTaskStatus.Completed);
        TaskCompletionRate = totalTasks == 0 ? 0 : (int)(completedTasks * 100.0 / totalTasks);

        var totalAudits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId);
        var completedAudits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && a.Status == AuditStatus.Completed);
        AuditCoveragePercent = totalAudits == 0 ? 0 : (int)(completedAudits * 100.0 / totalAudits);
        AuditFindingsCount = await _dbContext.AuditFindings.AsNoTracking()
            .Join(_dbContext.Audits.Where(a => a.TenantId == tenantId), f => f.AuditId, a => a.Id, (f, a) => f)
            .CountAsync();

        var risks = await _dbContext.RiskAssessments.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status != RiskStatus.Closed)
            .Select(r => r.RiskScore).ToListAsync();
        AvgRiskScore = risks.Count == 0 ? 0 : Math.Round(risks.Average(), 1);

        var trainingRecords = await _dbContext.TrainingRecords.AsNoTracking()
            .Where(t => t.TenantId == tenantId).ToListAsync();
        var totalTraining = trainingRecords.Count;
        var completedTraining = trainingRecords.Count(t => t.Status == TrainingStatus.Completed);
        TrainingComplianceRate = totalTraining == 0 ? 0 : (int)(completedTraining * 100.0 / totalTraining);
    }

    private async Task BuildAllCharts(Guid tenantId, List<Guid>? unitUserIds, Guid? orgUnitId)
    {
        DocStatusChartJson = await BuildDocStatusChartAsync(tenantId, unitUserIds);
        MonthlyTrendJson = await BuildMonthlyTrendAsync(tenantId, unitUserIds, orgUnitId);
        CapaAgingJson = await BuildCapaAgingAsync(tenantId, unitUserIds);
        RiskHeatMapJson = await BuildRiskHeatMapAsync(tenantId);
        SupplierPerformanceJson = await BuildSupplierPerformanceAsync(tenantId);
        FindingSeverityJson = await BuildFindingSeverityAsync(tenantId);
    }

    private async Task<string> BuildDocStatusChartAsync(Guid tenantId, List<Guid>? unitUserIds)
    {
        var query = _dbContext.Documents.AsNoTracking().Where(d => d.TenantId == tenantId);
        if (unitUserIds != null) query = query.Where(d => unitUserIds.Contains(d.CreatedById));

        var grouped = await query.GroupBy(d => d.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();

        var statuses = new[] { DocumentStatus.Draft, DocumentStatus.Submitted, DocumentStatus.InReview, DocumentStatus.Approved, DocumentStatus.Rejected, DocumentStatus.Archived };
        return JsonSerializer.Serialize(new
        {
            labels = statuses.Select(s => s.ToString()),
            values = statuses.Select(s => grouped.FirstOrDefault(g => g.Status == s)?.Count ?? 0)
        }, _jsonOpts);
    }

    private async Task<string> BuildMonthlyTrendAsync(Guid tenantId, List<Guid>? unitUserIds, Guid? orgUnitId)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-11), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 12).Select(i => start.AddMonths(i)).ToList();

        var docsQuery = _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedAt >= start);
        var tasksQuery = _dbContext.QmsTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.CreatedAt >= start);
        if (unitUserIds != null)
        {
            docsQuery = docsQuery.Where(d => unitUserIds.Contains(d.CreatedById));
            tasksQuery = tasksQuery.Where(t => t.AssignedToId.HasValue && unitUserIds.Contains(t.AssignedToId.Value));
        }

        var docGrouped = await docsQuery.GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();
        var taskGrouped = await tasksQuery.GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).ToListAsync();

        return JsonSerializer.Serialize(new
        {
            labels = months.Select(m => m.ToString("MMM yyyy")),
            documents = months.Select(m => docGrouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0),
            tasks = months.Select(m => taskGrouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0)
        }, _jsonOpts);
    }

    private async Task<string> BuildCapaAgingAsync(Guid tenantId, List<Guid>? unitUserIds)
    {
        var query = _dbContext.Capas.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified);
        if (unitUserIds != null) query = query.Where(c => c.OwnerId.HasValue && unitUserIds.Contains(c.OwnerId.Value));

        var openCapas = await query.Select(c => c.CreatedAt).ToListAsync();
        var now = DateTime.UtcNow;
        var under30 = openCapas.Count(d => (now - d).TotalDays < 30);
        var d30to60 = openCapas.Count(d => (now - d).TotalDays >= 30 && (now - d).TotalDays < 60);
        var d60to90 = openCapas.Count(d => (now - d).TotalDays >= 60 && (now - d).TotalDays < 90);
        var over90 = openCapas.Count(d => (now - d).TotalDays >= 90);

        return JsonSerializer.Serialize(new
        {
            labels = new[] { "< 30 days", "30–60 days", "60–90 days", "> 90 days" },
            values = new[] { under30, d30to60, d60to90, over90 }
        }, _jsonOpts);
    }

    private async Task<string> BuildRiskHeatMapAsync(Guid tenantId)
    {
        var risks = await _dbContext.RiskAssessments.AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.Status != RiskStatus.Closed)
            .Select(r => new { r.Likelihood, r.Impact }).ToListAsync();

        // 5x5 grid: likelihood (1-5) x impact (1-5)
        var grid = new int[5, 5];
        foreach (var r in risks)
        {
            var li = Math.Clamp(r.Likelihood - 1, 0, 4);
            var im = Math.Clamp(r.Impact - 1, 0, 4);
            grid[li, im]++;
        }

        var cells = new List<object>();
        for (int l = 0; l < 5; l++)
            for (int i = 0; i < 5; i++)
                cells.Add(new { likelihood = l + 1, impact = i + 1, count = grid[l, i] });

        return JsonSerializer.Serialize(cells, _jsonOpts);
    }

    private async Task<string> BuildSupplierPerformanceAsync(Guid tenantId)
    {
        var suppliers = await _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive)
            .OrderByDescending(s => s.PerformanceScore)
            .Take(10)
            .Select(s => new { s.Name, s.PerformanceScore }).ToListAsync();

        return JsonSerializer.Serialize(new
        {
            labels = suppliers.Select(s => s.Name),
            values = suppliers.Select(s => s.PerformanceScore)
        }, _jsonOpts);
    }

    private async Task<string> BuildFindingSeverityAsync(Guid tenantId)
    {
        var grouped = await _dbContext.AuditFindings.AsNoTracking()
            .Join(_dbContext.Audits.Where(a => a.TenantId == tenantId), f => f.AuditId, a => a.Id, (f, a) => f)
            .GroupBy(f => f.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() }).ToListAsync();

        return JsonSerializer.Serialize(new
        {
            labels = new[] { "Minor", "Major", "Critical", "Observation" },
            values = new[]
            {
                grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Minor)?.Count ?? 0,
                grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Major)?.Count ?? 0,
                grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Critical)?.Count ?? 0,
                grouped.FirstOrDefault(g => g.Severity == FindingSeverity.Observation)?.Count ?? 0
            }
        }, _jsonOpts);
    }

    private async Task<List<DepartmentMetric>> BuildDepartmentMetricsAsync(Guid tenantId, Guid? filterOrgUnitId)
    {
        var orgUnitsQuery = _dbContext.OrganizationUnits.AsNoTracking().Where(o => o.TenantId == tenantId);
        if (filterOrgUnitId.HasValue) orgUnitsQuery = orgUnitsQuery.Where(o => o.Id == filterOrgUnitId.Value);
        var orgUnits = await orgUnitsQuery.ToListAsync();

        var users = await _dbContext.Users.AsNoTracking().Where(u => u.TenantId == tenantId)
            .Select(u => new { u.Id, u.OrganizationUnitId }).ToListAsync();
        var docs = await _dbContext.Documents.AsNoTracking().Where(d => d.TenantId == tenantId)
            .Select(d => new { d.CreatedById, d.Status }).ToListAsync();
        var capas = await _dbContext.Capas.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified)
            .Select(c => c.OwnerId).ToListAsync();

        var metrics = new List<DepartmentMetric>();
        foreach (var unit in orgUnits)
        {
            var ids = users.Where(u => u.OrganizationUnitId == unit.Id).Select(u => u.Id).ToList();
            var unitDocs = docs.Where(d => ids.Contains(d.CreatedById)).ToList();
            var unitApproved = unitDocs.Count(d => d.Status == DocumentStatus.Approved);
            var compliance = unitDocs.Count == 0 ? 0 : (int)(unitApproved * 100.0 / unitDocs.Count);
            var openCapa = capas.Count(c => c.HasValue && ids.Contains(c.Value));
            var readiness = compliance >= 90 && openCapa <= 2 ? "High" : compliance >= 75 ? "Medium" : "Low";
            metrics.Add(new DepartmentMetric(unit.Name, $"{compliance}%", openCapa.ToString(), readiness));
        }
        return metrics;
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public record DepartmentMetric(string Department, string Compliance, string OpenCapa, string Readiness);
}
