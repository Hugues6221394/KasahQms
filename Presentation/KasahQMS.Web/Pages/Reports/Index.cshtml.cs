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

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService, IHierarchyService hierarchyService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
    }

    public List<ReportItem> Reports { get; set; } = new();
    public bool IsDepartmentManager { get; set; }
    public string? DepartmentName { get; set; }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var user = userId.HasValue 
            ? await _dbContext.Users.AsNoTracking().Include(u => u.Roles).Include(u => u.OrganizationUnit).FirstOrDefaultAsync(u => u.Id == userId.Value)
            : null;

        var roles = user?.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrAdmin = roles.Any(r => r == "TMD" || r == "Deputy Country Manager" || r.Contains("Admin"));
        IsDepartmentManager = !isTmdOrAdmin && roles.Any(r => r.Contains("Manager"));
        DepartmentName = user?.OrganizationUnit?.Name;

        var lastApproved = await _dbContext.Documents
            .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Approved && d.ApprovedAt.HasValue)
            .MaxAsync(d => (DateTime?)d.ApprovedAt);
        var lastAudit = await _dbContext.Audits
            .Where(a => a.TenantId == tenantId && a.Status == AuditStatus.Completed && a.ActualEndDate.HasValue)
            .MaxAsync(a => (DateTime?)a.ActualEndDate);
        var lastCapa = await _dbContext.Capas
            .Where(c => c.TenantId == tenantId && c.Status == CapaStatus.Closed && c.ActualCompletionDate.HasValue)
            .MaxAsync(c => (DateTime?)c.ActualCompletionDate);
        var lastDocument = await _dbContext.Documents
            .Where(d => d.TenantId == tenantId)
            .MaxAsync(d => (DateTime?)d.CreatedAt);
        var lastTask = await _dbContext.QmsTasks
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (DateTime?)t.CreatedAt);

        Reports = new List<ReportItem>
        {
            new("compliance", "Compliance Snapshot", "Executive compliance scorecard", "Quality Office", FormatDate(lastApproved), lastApproved.HasValue ? "Ready" : "Draft"),
            new("audits", "Audit Evidence Pack", "Internal audit evidence export", "Audit Team", FormatDate(lastAudit), lastAudit.HasValue ? "Ready" : "Draft"),
            new("capas", "CAPA Effectiveness", "CAPA closure effectiveness report", "Operations", FormatDate(lastCapa), lastCapa.HasValue ? "Ready" : "Draft"),
            new("docs", "Document Control Log", "Issued documents and approvals", "Document Control", FormatDate(lastDocument), lastDocument.HasValue ? "Ready" : "Draft"),
            new("tasks", "Task Summary", "Task completion and status report", "Operations", FormatDate(lastTask), lastTask.HasValue ? "Ready" : "Draft")
        };
    }

    public async Task<IActionResult> OnGetExportExcelAsync(string id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Get user's visible scope
        var user = userId.HasValue 
            ? await _dbContext.Users.AsNoTracking().Include(u => u.Roles).Include(u => u.OrganizationUnit).FirstOrDefaultAsync(u => u.Id == userId.Value)
            : null;

        var roles = user?.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrAdmin = roles.Any(r => r == "TMD" || r == "Deputy Country Manager" || r.Contains("Admin"));
        var isDepartmentManager = !isTmdOrAdmin && roles.Any(r => r.Contains("Manager"));

        // Get visible user IDs for filtering (department managers see only their dept)
        IEnumerable<Guid> visibleUserIds = new List<Guid>();
        if (isDepartmentManager && userId.HasValue)
        {
            visibleUserIds = await _hierarchyService.GetVisibleUserIdsAsync(userId.Value);
        }

        var csv = new StringBuilder();
        var fileName = $"Report_{id}_{DateTime.Now:yyyyMMdd}.csv";

        switch (id)
        {
            case "compliance":
                csv.AppendLine("Document Number,Title,Status,Created By,Approved At,Approved By");
                var docs = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Approved)
                    .Include(d => d.ApprovedBy)
                    .OrderByDescending(d => d.ApprovedAt)
                    .Take(500)
                    .ToListAsync();
                
                if (isDepartmentManager)
                    docs = docs.Where(d => visibleUserIds.Contains(d.CreatedById)).ToList();

                foreach (var d in docs)
                {
                    csv.AppendLine($"\"{d.DocumentNumber}\",\"{d.Title}\",\"{d.Status}\",\"{d.CreatedById}\",\"{d.ApprovedAt:yyyy-MM-dd}\",\"{d.ApprovedBy?.FullName ?? "—"}\"");
                }
                break;

            case "audits":
                csv.AppendLine("Audit Number,Title,Status,Lead Auditor,Start Date,End Date,Findings");
                var audits = await _dbContext.Audits.AsNoTracking()
                    .Where(a => a.TenantId == tenantId)
                    .Include(a => a.LeadAuditor)
                    .Include(a => a.Findings)
                    .OrderByDescending(a => a.PlannedStartDate)
                    .Take(200)
                    .ToListAsync();

                if (isDepartmentManager)
                    audits = audits.Where(a => a.LeadAuditorId.HasValue && visibleUserIds.Contains(a.LeadAuditorId.Value)).ToList();

                foreach (var a in audits)
                {
                    csv.AppendLine($"\"{a.AuditNumber}\",\"{a.Title}\",\"{a.Status}\",\"{a.LeadAuditor?.FullName ?? "—"}\",\"{a.PlannedStartDate:yyyy-MM-dd}\",\"{a.ActualEndDate:yyyy-MM-dd}\",\"{a.Findings?.Count ?? 0}\"");
                }
                break;

            case "capas":
                csv.AppendLine("CAPA Number,Title,Type,Status,Owner,Target Date,Actual Completion,Root Cause");
                var capas = await _dbContext.Capas.AsNoTracking()
                    .Where(c => c.TenantId == tenantId)
                    .Include(c => c.Owner)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(300)
                    .ToListAsync();

                if (isDepartmentManager)
                    capas = capas.Where(c => c.OwnerId.HasValue && visibleUserIds.Contains(c.OwnerId.Value)).ToList();

                foreach (var c in capas)
                {
                    csv.AppendLine($"\"{c.CapaNumber}\",\"{c.Title}\",\"{c.CapaType}\",\"{c.Status}\",\"{c.Owner?.FullName ?? "—"}\",\"{c.TargetCompletionDate:yyyy-MM-dd}\",\"{c.ActualCompletionDate:yyyy-MM-dd}\",\"{c.RootCauseAnalysis?.Replace("\"", "'")}\"");
                }
                break;

            case "docs":
                csv.AppendLine("Document Number,Title,Status,Type,Created,Created By,Current Version");
                var allDocs = await _dbContext.Documents.AsNoTracking()
                    .Where(d => d.TenantId == tenantId && !d.IsTemplate)
                    .Include(d => d.DocumentType)
                    .OrderByDescending(d => d.CreatedAt)
                    .Take(500)
                    .ToListAsync();

                if (isDepartmentManager)
                    allDocs = allDocs.Where(d => visibleUserIds.Contains(d.CreatedById)).ToList();

                foreach (var d in allDocs)
                {
                    csv.AppendLine($"\"{d.DocumentNumber}\",\"{d.Title}\",\"{d.Status}\",\"{d.DocumentType?.Name ?? "—"}\",\"{d.CreatedAt:yyyy-MM-dd}\",\"{d.CreatedById}\",\"{d.CurrentVersion}\"");
                }
                break;

            case "tasks":
                csv.AppendLine("Task Number,Title,Status,Priority,Assigned To,Due Date,Completed At,Created At");
                var tasks = await _dbContext.QmsTasks.AsNoTracking()
                    .Where(t => t.TenantId == tenantId)
                    .Include(t => t.AssignedTo)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(500)
                    .ToListAsync();

                if (isDepartmentManager)
                    tasks = tasks.Where(t => t.AssignedToId.HasValue && visibleUserIds.Contains(t.AssignedToId.Value)).ToList();

                foreach (var t in tasks)
                {
                    csv.AppendLine($"\"{t.TaskNumber}\",\"{t.Title}\",\"{t.Status}\",\"{t.Priority}\",\"{t.AssignedTo?.FullName ?? "—"}\",\"{t.DueDate:yyyy-MM-dd}\",\"{t.CompletedAt:yyyy-MM-dd}\",\"{t.CreatedAt:yyyy-MM-dd}\"");
                }
                break;

            default:
                return Content("Unknown report type.");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var result = new byte[bom.Length + bytes.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(bytes, 0, result, bom.Length, bytes.Length);

        return File(result, "text/csv", fileName);
    }
    
    public IActionResult OnGetExportPdf(string id)
    {
        // For now, redirect to a print-friendly page
        return RedirectToPage("PrintView", new { id });
    }

    public record ReportItem(string Id, string Title, string Description, string Owner, string LastRun, string Status);

    private static string FormatDate(DateTime? date)
    {
        return date?.ToString("MMM dd, yyyy") ?? "Not run";
    }
}


