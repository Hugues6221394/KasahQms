using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Departments;

/// <summary>
/// Organization Unit (Department) management page. Only System Admin can manage departments.
/// Per QMS requirements, System Admin creates Organization Units (e.g., Rwanda, Legal Department, Finance Department).
/// </summary>
[Authorize(Roles = "System Admin,SystemAdmin,Admin,TenantAdmin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public List<DepartmentRow> Departments { get; set; } = new();
    public int TotalDepartments { get; set; }
    public int ActiveDepartments { get; set; }

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var managers = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => new { u.Id, u.FullName, u.OrganizationUnitId, Roles = u.Roles })
            .ToListAsync();

        var departmentsList = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.Parent)
            .OrderBy(o => o.Name)
            .ToListAsync();

        // Get user counts per department
        var userCounts = await _dbContext.Users
            .Where(u => u.TenantId == tenantId && u.OrganizationUnitId.HasValue)
            .GroupBy(u => u.OrganizationUnitId!.Value)
            .Select(g => new { DepartmentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.DepartmentId, x => x.Count);

        Departments = departmentsList
            .Select(o => new DepartmentRow(
                o.Id,
                o.Name,
                o.Code,
                o.Description,
                o.Parent?.Name,
                managers.FirstOrDefault(m =>
                    m.OrganizationUnitId == o.Id &&
                    m.Roles != null &&
                    m.Roles.Any(r => r.Name.Contains("Manager")))?.FullName ?? "Unassigned",
                userCounts.GetValueOrDefault(o.Id, 0),
                o.IsActive,
                o.IsActive ? "Active" : "Inactive"))
            .ToList();

        TotalDepartments = Departments.Count;
        ActiveDepartments = Departments.Count(d => d.IsActive);

        _logger.LogInformation("Department list accessed by admin {UserId}. Showing {Count} departments.", 
            _currentUserService.UserId, Departments.Count);
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id)
    {
        var department = await _dbContext.OrganizationUnits.FindAsync(id);
        if (department == null)
        {
            TempData["Error"] = "Department not found.";
            return RedirectToPage();
        }

        department.IsActive = false;

        await _auditLogService.LogAsync(
            "DEPARTMENT_DEACTIVATED",
            "OrganizationUnit",
            department.Id,
            $"Department '{department.Name}' deactivated by admin");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Department {DepartmentId} deactivated by admin {AdminId}", 
            id, _currentUserService.UserId);
        
        TempData["Success"] = $"Department '{department.Name}' has been deactivated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(Guid id)
    {
        var department = await _dbContext.OrganizationUnits.FindAsync(id);
        if (department == null)
        {
            TempData["Error"] = "Department not found.";
            return RedirectToPage();
        }

        department.IsActive = true;

        await _auditLogService.LogAsync(
            "DEPARTMENT_ACTIVATED",
            "OrganizationUnit",
            department.Id,
            $"Department '{department.Name}' activated by admin");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Department {DepartmentId} activated by admin {AdminId}", 
            id, _currentUserService.UserId);
        
        TempData["Success"] = $"Department '{department.Name}' has been activated.";
        return RedirectToPage();
    }

    public record DepartmentRow(
        Guid Id,
        string Name, 
        string Code, 
        string? Description,
        string? ParentName,
        string Manager, 
        int UserCount,
        bool IsActive,
        string Status);
}


