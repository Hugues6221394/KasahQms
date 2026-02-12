using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Reports;

public class PrintViewModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public PrintViewModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string ReportTitle { get; set; } = "Report";
    public DateTime GeneratedDate { get; set; }
    public List<Dictionary<string, string>> ReportData { get; set; } = new();

    public async Task OnGetAsync(string id)
    {
        GeneratedDate = DateTime.UtcNow;
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (id == "audits")
        {
            ReportTitle = "Audit Evidence Report";
            var data = await _dbContext.Audits.AsNoTracking()
                .Where(a => a.TenantId == tenantId)
                .Select(a => new { a.AuditNumber, a.Title, a.Status, Start = a.PlannedStartDate.ToString("d"), End = a.PlannedEndDate.ToString("d") })
                .ToListAsync();
            
            ReportData = data.Select(d => new Dictionary<string, string>
            {
                ["Number"] = d.AuditNumber,
                ["Title"] = d.Title,
                ["Status"] = d.Status.ToString(),
                ["Start Date"] = d.Start,
                ["End Date"] = d.End
            }).ToList();
        }
        else if (id == "capas")
        {
            ReportTitle = "CAPA Effectiveness Report";
            var data = await _dbContext.Capas.AsNoTracking()
                .Where(c => c.TenantId == tenantId)
                .Select(c => new { c.CapaNumber, c.Title, c.Status, Created = c.CreatedAt.ToString("d"), Owner = c.Owner != null ? c.Owner.FullName : "Unassigned" })
                .ToListAsync();

            ReportData = data.Select(d => new Dictionary<string, string>
            {
                ["Number"] = d.CapaNumber,
                ["Title"] = d.Title,
                ["Status"] = d.Status.ToString(),
                ["Date"] = d.Created,
                ["Owner"] = d.Owner
            }).ToList();
        }
        else
        {
            ReportTitle = "Compliance Snapshot";
            var data = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.TenantId == tenantId)
                .Select(d => new { d.DocumentNumber, d.Title, d.Status, Ver = d.CurrentVersion, Created = d.CreatedAt.ToString("d") })
                .ToListAsync();

            ReportData = data.Select(d => new Dictionary<string, string>
            {
                ["Number"] = d.DocumentNumber,
                ["Title"] = d.Title,
                ["Status"] = d.Status.ToString(),
                ["Version"] = d.Ver.ToString(),
                ["Date"] = d.Created
            }).ToList();
        }
    }
}
