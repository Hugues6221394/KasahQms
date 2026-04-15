using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using AppAuthorizationService = KasahQMS.Application.Common.Security.IAuthorizationService;

namespace KasahQMS.Web.Pages.Departments;

[Microsoft.AspNetCore.Authorization.Authorize]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly AppAuthorizationService _authorizationService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        AppAuthorizationService authorizationService,
        IAuditLogService auditLogService,
        ILogger<EditModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Department name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Department code is required")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "Code must be between 2 and 20 characters")]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [BindProperty]
    public Guid? ParentId { get; set; }

    [BindProperty]
    public bool IsActive { get; set; }

    public List<CreateModel.LookupItem> ParentUnits { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanEditDepartmentAsync())
            return Forbid();

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var department = await _dbContext.OrganizationUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == Id && o.TenantId == tenantId);
        if (department == null) return RedirectToPage("./Index");

        Name = department.Name;
        Code = department.Code;
        Description = department.Description;
        ParentId = department.ParentId;
        IsActive = department.IsActive;

        await LoadParentsAsync(tenantId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CanEditDepartmentAsync())
            return Forbid();

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var department = await _dbContext.OrganizationUnits
            .FirstOrDefaultAsync(o => o.Id == Id && o.TenantId == tenantId);
        if (department == null) return RedirectToPage("./Index");

        var duplicateName = await _dbContext.OrganizationUnits.AnyAsync(o =>
            o.TenantId == tenantId && o.Id != Id && o.Name.ToLower() == Name.ToLower());
        if (duplicateName)
            ModelState.AddModelError(nameof(Name), "A department with this name already exists.");

        var duplicateCode = await _dbContext.OrganizationUnits.AnyAsync(o =>
            o.TenantId == tenantId && o.Id != Id && o.Code.ToLower() == Code.ToLower());
        if (duplicateCode)
            ModelState.AddModelError(nameof(Code), "A department with this code already exists.");

        if (ParentId == Id)
            ModelState.AddModelError(nameof(ParentId), "Department cannot be its own parent.");

        if (!ModelState.IsValid)
        {
            await LoadParentsAsync(tenantId);
            return Page();
        }

        department.Name = Name.Trim();
        department.Code = Code.Trim();
        department.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        department.ParentId = ParentId;
        department.IsActive = IsActive;
        department.LastModifiedById = _currentUserService.UserId;
        department.LastModifiedAt = DateTime.UtcNow;

        await _auditLogService.LogAsync(
            "DEPARTMENT_UPDATED",
            "OrganizationUnit",
            department.Id,
            $"Department '{department.Name}' updated by admin");

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Department {DepartmentId} updated by {UserId}", department.Id, _currentUserService.UserId);
        TempData["Success"] = $"Department '{department.Name}' updated successfully.";
        return RedirectToPage("./Index");
    }

    private async Task LoadParentsAsync(Guid tenantId)
    {
        ParentUnits = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.Id != Id)
            .OrderBy(o => o.Name)
            .Select(o => new CreateModel.LookupItem(o.Id, o.Name, o.Code))
            .ToListAsync();
    }

    private async Task<bool> CanEditDepartmentAsync()
    {
        if (await _authorizationService.HasPermissionAsync(Permissions.Organization.Edit))
        {
            return true;
        }

        if (!_currentUserService.UserId.HasValue)
        {
            return false;
        }

        var roles = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Id == _currentUserService.UserId.Value)
            .SelectMany(u => u.Roles)
            .Select(r => r.Name)
            .ToListAsync();

        return roles.Any(r =>
            r.Contains("System Admin", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("TenantAdmin", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Tenant Admin", StringComparison.OrdinalIgnoreCase));
    }
}
