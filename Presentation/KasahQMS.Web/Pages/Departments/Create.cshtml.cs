using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Departments;

/// <summary>
/// Create new Organization Unit (Department) page. Only System Admin can create departments.
/// Per QMS requirements, System Admin creates Organization Units (e.g., Rwanda, Legal Department, Finance Department).
/// </summary>
[Authorize(Roles = "System Admin,SystemAdmin,Admin,TenantAdmin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext, 
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

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

    public List<LookupItem> ParentUnits { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadParentsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadParentsAsync();

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Check for duplicate name
        var nameExists = await _dbContext.OrganizationUnits.AnyAsync(o => 
            o.TenantId == tenantId && 
            o.Name.ToLower() == Name.ToLower());

        if (nameExists)
        {
            ModelState.AddModelError(nameof(Name), "A department with this name already exists.");
            return Page();
        }

        // Check for duplicate code
        var codeExists = await _dbContext.OrganizationUnits.AnyAsync(o => 
            o.TenantId == tenantId && 
            o.Code.ToLower() == Code.ToLower());

        if (codeExists)
        {
            ModelState.AddModelError(nameof(Code), "A department with this code already exists.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var createdBy = _currentUserService.UserId ?? Guid.Empty;

            var unit = KasahQMS.Domain.Entities.Identity.OrganizationUnit.Create(
                tenantId,
                Name,
                Code,
                Description,
                ParentId,
                createdBy);

            _dbContext.OrganizationUnits.Add(unit);
            await _dbContext.SaveChangesAsync();

            // Log department creation
            var parentName = ParentId.HasValue 
                ? await _dbContext.OrganizationUnits.Where(o => o.Id == ParentId.Value).Select(o => o.Name).FirstOrDefaultAsync()
                : "None";

            await _auditLogService.LogAsync(
                "DEPARTMENT_CREATED",
                "OrganizationUnit",
                unit.Id,
                $"Department '{unit.Name}' (Code: {unit.Code}) created. Parent: {parentName}");

            _logger.LogInformation(
                "Department {DepartmentId} ({DepartmentName}) created by admin {AdminId}", 
                unit.Id, unit.Name, createdBy);

            TempData["Success"] = $"Department '{unit.Name}' created successfully.";
            return RedirectToPage("/Departments/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create department {DepartmentName}", Name);
            ModelState.AddModelError(string.Empty, "An error occurred while creating the department. Please try again.");
            return Page();
        }
    }

    private async Task LoadParentsAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        ParentUnits = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new LookupItem(o.Id, o.Name, o.Code))
            .ToListAsync();
    }

    public record LookupItem(Guid Id, string Name, string Code);
}
