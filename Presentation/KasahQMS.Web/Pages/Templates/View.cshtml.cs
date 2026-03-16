using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Templates;

[Authorize]
public class ViewModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ViewModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public TemplateDetail? Template { get; set; }
    public string AuthorizedDepartments { get; set; } = "All Departments";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var doc = await _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Where(d => d.Id == id && d.TenantId == tenantId && d.IsTemplate)
            .Select(d => new
            {
                d.Id, d.DocumentNumber, d.Title, d.Description, d.Content,
                d.Status, d.CreatedAt, d.AuthorizedDepartmentIds,
                DocumentType = d.DocumentType != null ? d.DocumentType.Name : "—"
            })
            .FirstOrDefaultAsync();

        if (doc == null)
            return NotFound();

        Template = new TemplateDetail(
            doc.Id, doc.DocumentNumber, doc.Title, doc.Description,
            doc.DocumentType, doc.Content, doc.Status.ToString(),
            doc.CreatedAt.ToString("MMM dd, yyyy"), doc.AuthorizedDepartmentIds ?? "");

        // Resolve department names
        if (!string.IsNullOrWhiteSpace(doc.AuthorizedDepartmentIds))
        {
            var ids = doc.AuthorizedDepartmentIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(s => Guid.TryParse(s.Trim(), out _))
                .Select(s => Guid.Parse(s.Trim()))
                .ToList();

            if (ids.Any())
            {
                var names = await _dbContext.OrganizationUnits
                    .AsNoTracking()
                    .Where(o => ids.Contains(o.Id))
                    .Select(o => o.Name)
                    .ToListAsync();

                AuthorizedDepartments = names.Any() ? string.Join(", ", names) : "Specified Departments";
            }
        }

        return Page();
    }

    public record TemplateDetail(
        Guid Id,
        string DocumentNumber,
        string Title,
        string? Description,
        string DocumentType,
        string? Content,
        string Status,
        string CreatedAt,
        string AuthorizedDeptIds);
}
