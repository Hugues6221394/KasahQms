using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Departments;

/// <summary>
/// Organization Unit (Department) management page.
/// System Admin: full CRUD. TMD/Deputy: read-only.
/// </summary>
[Authorize]
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
    public bool CanManageDepartments { get; set; }
    public bool CanViewDepartments { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUser = await GetCurrentUserWithRolesAsync();
        if (currentUser == null) return Unauthorized();
        (CanViewDepartments, CanManageDepartments) = ResolveDepartmentAccess(currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>());
        if (!CanViewDepartments) return Forbid();

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
        return Page();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id)
    {
        var currentUser = await GetCurrentUserWithRolesAsync();
        if (currentUser == null) return Unauthorized();
        (_, CanManageDepartments) = ResolveDepartmentAccess(currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>());
        if (!CanManageDepartments) return Forbid();

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
        var currentUser = await GetCurrentUserWithRolesAsync();
        if (currentUser == null) return Unauthorized();
        (_, CanManageDepartments) = ResolveDepartmentAccess(currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>());
        if (!CanManageDepartments) return Forbid();

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

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var currentUser = await GetCurrentUserWithRolesAsync();
        if (currentUser == null) return Unauthorized();
        (_, CanManageDepartments) = ResolveDepartmentAccess(currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>());
        if (!CanManageDepartments) return Forbid();

        var department = await _dbContext.OrganizationUnits.FindAsync(id);
        if (department == null)
        {
            TempData["Error"] = "Department not found.";
            return RedirectToPage();
        }

        var hasUsers = await _dbContext.Users.AnyAsync(u => u.OrganizationUnitId == id && u.IsActive);
        if (hasUsers)
        {
            TempData["Error"] = "Cannot delete a department with active users assigned.";
            return RedirectToPage();
        }

        var hasChildren = await _dbContext.OrganizationUnits.AnyAsync(o => o.ParentId == id && o.IsActive);
        if (hasChildren)
        {
            TempData["Error"] = "Cannot delete a department that has child departments.";
            return RedirectToPage();
        }

        _dbContext.OrganizationUnits.Remove(department);
        await _auditLogService.LogAsync(
            "DEPARTMENT_DELETED",
            "OrganizationUnit",
            department.Id,
            $"Department '{department.Name}' deleted by admin");
        await _dbContext.SaveChangesAsync();

        TempData["Success"] = $"Department '{department.Name}' deleted successfully.";
        return RedirectToPage();
    }

    private async Task<KasahQMS.Domain.Entities.Identity.User?> GetCurrentUserWithRolesAsync()
    {
        if (!_currentUserService.UserId.HasValue) return null;
        return await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
    }

    private static (bool CanView, bool CanManage) ResolveDepartmentAccess(List<string> roles)
    {
        var isSystemAdmin = roles.Any(r =>
            string.Equals(r, "System Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "TenantAdmin", StringComparison.OrdinalIgnoreCase));

        var isTmdOrDeputy = roles.Any(r =>
            string.Equals(r, "TMD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Country Manager", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase));

        return (isSystemAdmin || isTmdOrDeputy, isSystemAdmin);
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

