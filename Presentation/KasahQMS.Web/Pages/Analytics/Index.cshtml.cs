using System.Text.Json;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Analytics;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<KpiItem> Kpis { get; set; } = new();
    public List<DepartmentMetric> DepartmentMetrics { get; set; } = new();
    public string ComplianceTrendJson { get; set; } = "{}";
    public string FindingSeverityJson { get; set; } = "{}";

    public async Task OnGetAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var totalDocs = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId);
        var approvedDocs = await _dbContext.Documents.CountAsync(d => d.TenantId == tenantId && d.Status == DocumentStatus.Approved);
        var totalAudits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId);
        var completedAudits = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && a.Status == AuditStatus.Completed);
        var closedCapas = await _dbContext.Capas
            .Where(c => c.TenantId == tenantId &&
                        (c.Status == CapaStatus.Closed || c.Status == CapaStatus.EffectivenessVerified) &&
                        c.ActualCompletionDate.HasValue)
            .Select(c => new { c.CreatedAt, c.ActualCompletionDate })
            .ToListAsync();

        var trainingTasks = await _dbContext.QmsTasks.CountAsync(t => t.TenantId == tenantId && t.Title.ToLower().Contains("training"));
        var completedTraining = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId && t.Title.ToLower().Contains("training") && t.Status == QmsTaskStatus.Completed);

        Kpis = new List<KpiItem>
        {
            new("Enterprise compliance", totalDocs == 0 ? "0%" : $"{(int)(approvedDocs * 100.0 / totalDocs)}%", "Approved documents"),
            new("Audit completion rate", totalAudits == 0 ? "0%" : $"{(int)(completedAudits * 100.0 / totalAudits)}%", "Completion status"),
            new("Average CAPA closure", closedCapas.Count == 0 ? "0 days" :
                $"{(int)closedCapas.Average(c => (c.ActualCompletionDate!.Value - c.CreatedAt).TotalDays)} days", "Mean closure time"),
            new("Training coverage", trainingTasks == 0 ? "0%" : $"{(int)(completedTraining * 100.0 / trainingTasks)}%", "Training tasks")
        };

        DepartmentMetrics = await BuildDepartmentMetricsAsync(tenantId);
        ComplianceTrendJson = await BuildComplianceTrendAsync(tenantId);
        FindingSeverityJson = await BuildFindingSeverityAsync(tenantId);
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

    private async Task<List<DepartmentMetric>> BuildDepartmentMetricsAsync(Guid tenantId)
    {
        var orgUnits = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .ToListAsync();

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

    private async Task<string> BuildComplianceTrendAsync(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-5), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6).Select(i => start.AddMonths(i)).ToList();

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

    private async Task<string> BuildFindingSeverityAsync(Guid tenantId)
    {
        var groups = await _dbContext.AuditFindings.AsNoTracking()
            .Join(_dbContext.Audits.AsNoTracking().Where(a => a.TenantId == tenantId),
                f => f.AuditId,
                a => a.Id,
                (f, _) => f)
            .GroupBy(f => f.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync();

        var labels = new[] { "Minor", "Major", "Critical", "Observation" };
        var values = new[]
        {
            groups.FirstOrDefault(g => g.Severity == FindingSeverity.Minor)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Severity == FindingSeverity.Major)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Severity == FindingSeverity.Critical)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Severity == FindingSeverity.Observation)?.Count ?? 0
        };

        return SerializeChart(labels, values);
    }
}


