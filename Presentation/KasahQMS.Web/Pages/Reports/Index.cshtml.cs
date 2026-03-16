using System.Text;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Reports;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IExportService _exportService;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IExportService exportService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _exportService = exportService;
    }

    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReportType { get; set; }
    [BindProperty(SupportsGet = true)] public int Days { get; set; } = 30;

    public List<ReportCategory> Categories { get; set; } = new();
    public List<Dictionary<string, object>> PreviewRows { get; set; } = new();
    public List<string> PreviewColumns { get; set; } = new();
    public string SelectedReportTitle { get; set; } = "";

    public bool IsDepartmentManager { get; set; }
    public string? DepartmentName { get; set; }

    public async Task OnGetAsync()
    {
        BuildCategories();
        await DetermineScope();
        if (!string.IsNullOrEmpty(Category) && !string.IsNullOrEmpty(ReportType))
            await LoadPreviewAsync();
    }

    public async Task<IActionResult> OnPostExportAsync(string category, string reportType, int days, string format)
    {
        Category = category;
        ReportType = reportType;
        Days = days;
        await DetermineScope();
        await LoadPreviewAsync();

        if (PreviewRows.Count == 0)
            return Page();

        var fileName = $"{category}_{reportType}_{DateTime.Now:yyyyMMdd}";

        switch (format?.ToLower())
        {
            case "pdf":
                var pdfBytes = await _exportService.ExportToPdfAsync(SelectedReportTitle, PreviewRows);
                return File(pdfBytes, "application/pdf", $"{fileName}.pdf");

            case "excel":
                var excelBytes = await _exportService.ExportToExcelAsync(SelectedReportTitle, PreviewRows);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{fileName}.xlsx");

            case "csv":
            default:
                var csvBytes = await _exportService.ExportToCsvAsync(PreviewRows);
                return File(csvBytes, "text/csv", $"{fileName}.csv");
        }
    }

    private void BuildCategories()
    {
        Categories = new List<ReportCategory>
        {
            new("documents", "Documents", new[]
            {
                new ReportTypeItem("status-summary", "Status Summary"),
                new ReportTypeItem("approval-history", "Approval History"),
                new ReportTypeItem("overdue-documents", "Overdue Documents")
            }),
            new("tasks", "Tasks", new[]
            {
                new ReportTypeItem("task-status", "Task Status"),
                new ReportTypeItem("workload-by-user", "Workload by User"),
                new ReportTypeItem("overdue-tasks", "Overdue Tasks")
            }),
            new("capas", "CAPAs", new[]
            {
                new ReportTypeItem("status-distribution", "Status Distribution"),
                new ReportTypeItem("aging-report", "Aging Report"),
                new ReportTypeItem("effectiveness-summary", "Effectiveness Summary")
            }),
            new("audits", "Audits", new[]
            {
                new ReportTypeItem("coverage-report", "Coverage Report"),
                new ReportTypeItem("finding-summary", "Finding Summary")
            }),
            new("training", "Training", new[]
            {
                new ReportTypeItem("compliance-report", "Compliance Report"),
                new ReportTypeItem("expiring-certifications", "Expiring Certifications")
            }),
            new("risk", "Risk", new[]
            {
                new ReportTypeItem("risk-register", "Risk Register"),
                new ReportTypeItem("high-risk-items", "High-Risk Items")
            }),
            new("suppliers", "Suppliers", new[]
            {
                new ReportTypeItem("performance-summary", "Performance Summary"),
                new ReportTypeItem("audit-schedule", "Audit Schedule")
            })
        };
    }

    private async Task DetermineScope()
    {
        var userId = _currentUserService.UserId;
        var user = userId.HasValue
            ? await _dbContext.Users.AsNoTracking().Include(u => u.Roles).Include(u => u.OrganizationUnit)
                .FirstOrDefaultAsync(u => u.Id == userId.Value)
            : null;
        var roles = user?.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrAdmin = roles.Any(r => r == "TMD" || r == "Deputy Country Manager" || r.Contains("Admin"));
        IsDepartmentManager = !isTmdOrAdmin && roles.Any(r => r.Contains("Manager"));
        DepartmentName = user?.OrganizationUnit?.Name;
    }

    private async Task LoadPreviewAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var cutoff = DateTime.UtcNow.AddDays(-Days);
        IEnumerable<Guid> visibleUserIds = Array.Empty<Guid>();
        if (IsDepartmentManager && _currentUserService.UserId.HasValue)
            visibleUserIds = await _hierarchyService.GetVisibleUserIdsAsync(_currentUserService.UserId.Value);

        var key = $"{Category}|{ReportType}";
        SelectedReportTitle = $"{Category} — {ReportType}".Replace("-", " ");

        switch (key)
        {
            case "documents|status-summary":
                PreviewColumns = new() { "Document #", "Title", "Status", "Type", "Created" };
                var docs = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.TenantId == tenantId && d.CreatedAt >= cutoff)
                    .Include(d => d.DocumentType).OrderByDescending(d => d.CreatedAt).Take(200).ToListAsync();
                if (IsDepartmentManager) docs = docs.Where(d => visibleUserIds.Contains(d.CreatedById)).ToList();
                PreviewRows = docs.Select(d => new Dictionary<string, object>
                {
                    ["Document #"] = d.DocumentNumber, ["Title"] = d.Title, ["Status"] = d.Status.ToString(),
                    ["Type"] = d.DocumentType?.Name ?? "—", ["Created"] = d.CreatedAt.ToString("yyyy-MM-dd")
                }).ToList();
                break;

            case "documents|approval-history":
                PreviewColumns = new() { "Document #", "Title", "Approved At", "Approved By" };
                var approved = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Approved && d.ApprovedAt >= cutoff)
                    .Include(d => d.ApprovedBy).OrderByDescending(d => d.ApprovedAt).Take(200).ToListAsync();
                if (IsDepartmentManager) approved = approved.Where(d => visibleUserIds.Contains(d.CreatedById)).ToList();
                PreviewRows = approved.Select(d => new Dictionary<string, object>
                {
                    ["Document #"] = d.DocumentNumber, ["Title"] = d.Title,
                    ["Approved At"] = d.ApprovedAt?.ToString("yyyy-MM-dd") ?? "—",
                    ["Approved By"] = d.ApprovedBy?.FullName ?? "—"
                }).ToList();
                break;

            case "documents|overdue-documents":
                PreviewColumns = new() { "Document #", "Title", "Status", "Created", "Days Open" };
                var overdueDocs = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.TenantId == tenantId && d.Status != DocumentStatus.Approved && d.Status != DocumentStatus.Archived)
                    .OrderBy(d => d.CreatedAt).Take(200).ToListAsync();
                if (IsDepartmentManager) overdueDocs = overdueDocs.Where(d => visibleUserIds.Contains(d.CreatedById)).ToList();
                PreviewRows = overdueDocs.Select(d => new Dictionary<string, object>
                {
                    ["Document #"] = d.DocumentNumber, ["Title"] = d.Title, ["Status"] = d.Status.ToString(),
                    ["Created"] = d.CreatedAt.ToString("yyyy-MM-dd"),
                    ["Days Open"] = (int)(DateTime.UtcNow - d.CreatedAt).TotalDays
                }).ToList();
                break;

            case "tasks|task-status":
                PreviewColumns = new() { "Task #", "Title", "Status", "Priority", "Assigned To", "Due Date" };
                var tasks = await _dbContext.QmsTasks.AsNoTracking()
                    .Where(t => t.TenantId == tenantId && t.CreatedAt >= cutoff)
                    .Include(t => t.AssignedTo).OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync();
                if (IsDepartmentManager) tasks = tasks.Where(t => t.AssignedToId.HasValue && visibleUserIds.Contains(t.AssignedToId.Value)).ToList();
                PreviewRows = tasks.Select(t => new Dictionary<string, object>
                {
                    ["Task #"] = t.TaskNumber, ["Title"] = t.Title, ["Status"] = t.Status.ToString(),
                    ["Priority"] = t.Priority.ToString(), ["Assigned To"] = t.AssignedTo?.FullName ?? "—",
                    ["Due Date"] = t.DueDate?.ToString("yyyy-MM-dd") ?? "—"
                }).ToList();
                break;

            case "tasks|workload-by-user":
                PreviewColumns = new() { "User", "Total Tasks", "Open", "In Progress", "Completed" };
                var allTasks = await _dbContext.QmsTasks.AsNoTracking()
                    .Where(t => t.TenantId == tenantId && t.AssignedToId.HasValue && t.CreatedAt >= cutoff)
                    .Include(t => t.AssignedTo).ToListAsync();
                if (IsDepartmentManager) allTasks = allTasks.Where(t => visibleUserIds.Contains(t.AssignedToId!.Value)).ToList();
                PreviewRows = allTasks.GroupBy(t => t.AssignedTo?.FullName ?? "Unassigned").Select(g => new Dictionary<string, object>
                {
                    ["User"] = g.Key, ["Total Tasks"] = g.Count(),
                    ["Open"] = g.Count(t => t.Status == QmsTaskStatus.Open),
                    ["In Progress"] = g.Count(t => t.Status == QmsTaskStatus.InProgress),
                    ["Completed"] = g.Count(t => t.Status == QmsTaskStatus.Completed)
                }).ToList();
                break;

            case "tasks|overdue-tasks":
                PreviewColumns = new() { "Task #", "Title", "Assigned To", "Due Date", "Days Overdue" };
                var overdue = await _dbContext.QmsTasks.AsNoTracking()
                    .Where(t => t.TenantId == tenantId && t.Status == QmsTaskStatus.Overdue)
                    .Include(t => t.AssignedTo).OrderBy(t => t.DueDate).Take(200).ToListAsync();
                if (IsDepartmentManager) overdue = overdue.Where(t => t.AssignedToId.HasValue && visibleUserIds.Contains(t.AssignedToId.Value)).ToList();
                PreviewRows = overdue.Select(t => new Dictionary<string, object>
                {
                    ["Task #"] = t.TaskNumber, ["Title"] = t.Title,
                    ["Assigned To"] = t.AssignedTo?.FullName ?? "—",
                    ["Due Date"] = t.DueDate?.ToString("yyyy-MM-dd") ?? "—",
                    ["Days Overdue"] = t.DueDate.HasValue ? (int)(DateTime.UtcNow - t.DueDate.Value).TotalDays : 0
                }).ToList();
                break;

            case "capas|status-distribution":
                PreviewColumns = new() { "CAPA #", "Title", "Type", "Status", "Owner", "Target Date" };
                var capas = await _dbContext.Capas.AsNoTracking()
                    .Where(c => c.TenantId == tenantId && c.CreatedAt >= cutoff)
                    .Include(c => c.Owner).OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync();
                if (IsDepartmentManager) capas = capas.Where(c => c.OwnerId.HasValue && visibleUserIds.Contains(c.OwnerId.Value)).ToList();
                PreviewRows = capas.Select(c => new Dictionary<string, object>
                {
                    ["CAPA #"] = c.CapaNumber, ["Title"] = c.Title, ["Type"] = c.CapaType.ToString(),
                    ["Status"] = c.Status.ToString(), ["Owner"] = c.Owner?.FullName ?? "—",
                    ["Target Date"] = c.TargetCompletionDate?.ToString("yyyy-MM-dd") ?? "—"
                }).ToList();
                break;

            case "capas|aging-report":
                PreviewColumns = new() { "CAPA #", "Title", "Status", "Created", "Age (days)" };
                var openCapas = await _dbContext.Capas.AsNoTracking()
                    .Where(c => c.TenantId == tenantId && c.Status != CapaStatus.Closed && c.Status != CapaStatus.EffectivenessVerified)
                    .Include(c => c.Owner).OrderBy(c => c.CreatedAt).Take(200).ToListAsync();
                if (IsDepartmentManager) openCapas = openCapas.Where(c => c.OwnerId.HasValue && visibleUserIds.Contains(c.OwnerId.Value)).ToList();
                PreviewRows = openCapas.Select(c => new Dictionary<string, object>
                {
                    ["CAPA #"] = c.CapaNumber, ["Title"] = c.Title, ["Status"] = c.Status.ToString(),
                    ["Created"] = c.CreatedAt.ToString("yyyy-MM-dd"),
                    ["Age (days)"] = (int)(DateTime.UtcNow - c.CreatedAt).TotalDays
                }).ToList();
                break;

            case "capas|effectiveness-summary":
                PreviewColumns = new() { "CAPA #", "Title", "Root Cause", "Completed", "Days to Close" };
                var closedCapas = await _dbContext.Capas.AsNoTracking()
                    .Where(c => c.TenantId == tenantId && (c.Status == CapaStatus.Closed || c.Status == CapaStatus.EffectivenessVerified) && c.ActualCompletionDate >= cutoff)
                    .Include(c => c.Owner).OrderByDescending(c => c.ActualCompletionDate).Take(200).ToListAsync();
                if (IsDepartmentManager) closedCapas = closedCapas.Where(c => c.OwnerId.HasValue && visibleUserIds.Contains(c.OwnerId.Value)).ToList();
                PreviewRows = closedCapas.Select(c => new Dictionary<string, object>
                {
                    ["CAPA #"] = c.CapaNumber, ["Title"] = c.Title,
                    ["Root Cause"] = c.RootCauseAnalysis ?? "—",
                    ["Completed"] = c.ActualCompletionDate?.ToString("yyyy-MM-dd") ?? "—",
                    ["Days to Close"] = c.ActualCompletionDate.HasValue ? (int)(c.ActualCompletionDate.Value - c.CreatedAt).TotalDays : 0
                }).ToList();
                break;

            case "audits|coverage-report":
                PreviewColumns = new() { "Audit #", "Title", "Type", "Status", "Lead Auditor", "Start Date" };
                var audits = await _dbContext.Audits.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && a.PlannedStartDate >= cutoff)
                    .Include(a => a.LeadAuditor).OrderByDescending(a => a.PlannedStartDate).Take(200).ToListAsync();
                PreviewRows = audits.Select(a => new Dictionary<string, object>
                {
                    ["Audit #"] = a.AuditNumber, ["Title"] = a.Title, ["Type"] = a.AuditType.ToString(),
                    ["Status"] = a.Status.ToString(), ["Lead Auditor"] = a.LeadAuditor?.FullName ?? "—",
                    ["Start Date"] = a.PlannedStartDate.ToString("yyyy-MM-dd")
                }).ToList();
                break;

            case "audits|finding-summary":
                PreviewColumns = new() { "Audit #", "Finding", "Severity", "Status" };
                var findings = await _dbContext.AuditFindings.AsNoTracking()
                    .Join(_dbContext.Audits.Where(a => a.TenantId == tenantId), f => f.AuditId, a => a.Id, (f, a) => new { f, a })
                    .OrderByDescending(x => x.a.PlannedStartDate).Take(200).ToListAsync();
                PreviewRows = findings.Select(x => new Dictionary<string, object>
                {
                    ["Audit #"] = x.a.AuditNumber, ["Finding"] = x.f.Description ?? "—",
                    ["Severity"] = x.f.Severity.ToString(), ["Status"] = x.f.Status.ToString()
                }).ToList();
                break;

            case "training|compliance-report":
                PreviewColumns = new() { "Title", "User", "Type", "Status", "Completed", "Expiry" };
                var training = await _dbContext.TrainingRecords.AsNoTracking()
                    .Where(t => t.TenantId == tenantId).Include(t => t.User)
                    .OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync();
                PreviewRows = training.Select(t => new Dictionary<string, object>
                {
                    ["Title"] = t.Title, ["User"] = t.User?.FullName ?? "—",
                    ["Type"] = t.TrainingType.ToString(), ["Status"] = t.Status.ToString(),
                    ["Completed"] = t.CompletedDate?.ToString("yyyy-MM-dd") ?? "—",
                    ["Expiry"] = t.ExpiryDate?.ToString("yyyy-MM-dd") ?? "—"
                }).ToList();
                break;

            case "training|expiring-certifications":
                PreviewColumns = new() { "Title", "User", "Certificate #", "Expiry", "Days Until Expiry" };
                var expiring = await _dbContext.TrainingRecords.AsNoTracking()
                    .Where(t => t.TenantId == tenantId && t.ExpiryDate.HasValue && t.ExpiryDate <= DateTime.UtcNow.AddDays(90))
                    .Include(t => t.User).OrderBy(t => t.ExpiryDate).Take(200).ToListAsync();
                PreviewRows = expiring.Select(t => new Dictionary<string, object>
                {
                    ["Title"] = t.Title, ["User"] = t.User?.FullName ?? "—",
                    ["Certificate #"] = t.CertificateNumber ?? "—",
                    ["Expiry"] = t.ExpiryDate?.ToString("yyyy-MM-dd") ?? "—",
                    ["Days Until Expiry"] = t.ExpiryDate.HasValue ? (int)(t.ExpiryDate.Value - DateTime.UtcNow).TotalDays : 0
                }).ToList();
                break;

            case "risk|risk-register":
                PreviewColumns = new() { "Risk #", "Title", "Category", "Score", "Status", "Owner" };
                var risks = await _dbContext.RiskAssessments.AsNoTracking()
                    .Where(r => r.TenantId == tenantId).Include(r => r.Owner)
                    .OrderByDescending(r => r.RiskScore).Take(200).ToListAsync();
                PreviewRows = risks.Select(r => new Dictionary<string, object>
                {
                    ["Risk #"] = r.RiskNumber, ["Title"] = r.Title,
                    ["Category"] = r.Category ?? "—", ["Score"] = r.RiskScore,
                    ["Status"] = r.Status.ToString(), ["Owner"] = r.Owner?.FullName ?? "—"
                }).ToList();
                break;

            case "risk|high-risk-items":
                PreviewColumns = new() { "Risk #", "Title", "Score", "Mitigation Plan", "Review Date" };
                var highRisk = await _dbContext.RiskAssessments.AsNoTracking()
                    .Where(r => r.TenantId == tenantId && r.RiskScore >= 15 && r.Status != RiskStatus.Closed)
                    .OrderByDescending(r => r.RiskScore).Take(200).ToListAsync();
                PreviewRows = highRisk.Select(r => new Dictionary<string, object>
                {
                    ["Risk #"] = r.RiskNumber, ["Title"] = r.Title, ["Score"] = r.RiskScore,
                    ["Mitigation Plan"] = r.MitigationPlan ?? "—",
                    ["Review Date"] = r.ReviewDate?.ToString("yyyy-MM-dd") ?? "—"
                }).ToList();
                break;

            case "suppliers|performance-summary":
                PreviewColumns = new() { "Supplier", "Code", "Status", "Score", "Next Audit" };
                var suppliers = await _dbContext.Suppliers.AsNoTracking()
                    .Where(s => s.TenantId == tenantId && s.IsActive)
                    .OrderByDescending(s => s.PerformanceScore).Take(200).ToListAsync();
                PreviewRows = suppliers.Select(s => new Dictionary<string, object>
                {
                    ["Supplier"] = s.Name, ["Code"] = (object?)s.Code ?? "—",
                    ["Status"] = s.QualificationStatus.ToString(), ["Score"] = (object?)s.PerformanceScore ?? "—",
                    ["Next Audit"] = s.NextAuditDate?.ToString("yyyy-MM-dd") ?? "—"
                }).ToList();
                break;

            case "suppliers|audit-schedule":
                PreviewColumns = new() { "Supplier", "Audit Date", "Auditor", "Score", "Status" };
                var sAudits = await _dbContext.SupplierAudits.AsNoTracking()
                    .Where(sa => sa.TenantId == tenantId).Include(sa => sa.Supplier).Include(sa => sa.Auditor)
                    .OrderByDescending(sa => sa.AuditDate).Take(200).ToListAsync();
                PreviewRows = sAudits.Select(sa => new Dictionary<string, object>
                {
                    ["Supplier"] = sa.Supplier?.Name ?? "—",
                    ["Audit Date"] = sa.AuditDate.ToString("yyyy-MM-dd"),
                    ["Auditor"] = sa.Auditor?.FullName ?? "—",
                    ["Score"] = sa.Score, ["Status"] = sa.Status.ToString()
                }).ToList();
                break;
        }
    }

    public record ReportCategory(string Id, string Label, ReportTypeItem[] Types);
    public record ReportTypeItem(string Id, string Label);
}


