using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Templates;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public List<TemplateRow> Templates { get; set; } = new();
    public bool CanCreateTemplate { get; set; }
    public string UserRoleContext { get; set; } = "Staff";

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (userId == null)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var userOrgUnitId = user.OrganizationUnitId?.ToString() ?? "";

        // Determine role context
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        bool isDeputy = roles.Any(r => r.Contains("Deputy", StringComparison.OrdinalIgnoreCase));
        bool isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));
        bool isAuditor = roles.Any(r => r == "Auditor");

        if (isTmd || isAdmin) UserRoleContext = "TMD";
        else if (isDeputy) UserRoleContext = "Deputy";
        else if (isManager) UserRoleContext = "Manager";
        else if (isAuditor) UserRoleContext = "Auditor";
        else UserRoleContext = "Staff";

        // Only TMD/Admin can create templates
        CanCreateTemplate = isTmd || isAdmin;

        // Query templates
        var query = _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Include(d => d.Category)
            .Where(d => d.TenantId == tenantId && d.IsTemplate);

        // If not TMD/Admin, filter by authorized departments
        if (!isTmd && !isAdmin)
        {
            // User can see templates where their department is authorized
            query = query.Where(d => 
                string.IsNullOrEmpty(d.AuthorizedDepartmentIds) || // No restriction
                d.AuthorizedDepartmentIds.Contains(userOrgUnitId)); // Their dept is authorized
        }

        Templates = await query
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new TemplateRow(
                d.Id,
                d.DocumentNumber,
                d.Title,
                d.Description,
                d.DocumentType != null ? d.DocumentType.Name : "—",
                d.Status.ToString(),
                d.AuthorizedDepartmentIds ?? "",
                d.CreatedAt.ToString("MMM dd, yyyy")
            ))
            .ToListAsync();

        // Load department names for display
        var allDeptIds = Templates
            .Where(t => !string.IsNullOrEmpty(t.AuthorizedDeptIds))
            .SelectMany(t => t.AuthorizedDeptIds.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Where(id => Guid.TryParse(id.Trim(), out _))
            .Select(id => Guid.Parse(id.Trim()))
            .Distinct()
            .ToList();

        if (allDeptIds.Any())
        {
            DepartmentNames = await _dbContext.OrganizationUnits
                .AsNoTracking()
                .Where(o => allDeptIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id.ToString(), o => o.Name);
        }

        _logger.LogInformation("User {UserId} ({Role}) accessed Templates page. Found {Count} templates.",
            userId, UserRoleContext, Templates.Count);

        return Page();
    }

    public Dictionary<string, string> DepartmentNames { get; set; } = new();

    public string GetDepartmentDisplayNames(string authorizedDeptIds)
    {
        if (string.IsNullOrWhiteSpace(authorizedDeptIds))
            return "All Departments";

        var ids = authorizedDeptIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var names = ids
            .Select(id => DepartmentNames.TryGetValue(id.Trim(), out var name) ? name : null)
            .Where(n => n != null)
            .ToList();

        return names.Any() ? string.Join(", ", names) : "Specified Departments";
    }

    public record TemplateRow(
        Guid Id,
        string DocumentNumber,
        string Title,
        string? Description,
        string DocumentType,
        string Status,
        string AuthorizedDeptIds,
        string CreatedAt
    );
}
