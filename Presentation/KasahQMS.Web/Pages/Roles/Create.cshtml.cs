using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Roles;

/// <summary>
/// Create new role page. Only System Admin can create roles.
/// Per QMS requirements, System Admin defines:
/// - Roles and their names
/// - Base permissions per role
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
    [Required(ErrorMessage = "Role name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Role name must be between 2 and 100 characters")]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    [BindProperty]
    public List<string> SelectedPermissions { get; set; } = new();

    /// <summary>
    /// All available permissions grouped by module
    /// </summary>
    public Dictionary<string, List<PermissionItem>> PermissionGroups { get; set; } = new();

    public void OnGet()
    {
        LoadPermissions();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadPermissions();

        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Role name is required.");
            return Page();
        }

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Check for duplicate role name
        var exists = await _dbContext.Roles.AnyAsync(r => 
            r.TenantId == tenantId && 
            r.Name.ToLower() == Name.ToLower());

        if (exists)
        {
            ModelState.AddModelError(nameof(Name), "A role with this name already exists.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var permissions = SelectedPermissions
                .Select(p => Enum.TryParse<Permission>(p, out var perm) ? perm : Permission.None)
                .Where(p => p != Permission.None)
                .ToArray();

            var createdBy = _currentUserService.UserId ?? Guid.Empty;
            var role = KasahQMS.Domain.Entities.Identity.Role.Create(
                tenantId, Name, Description, permissions, createdBy);

            _dbContext.Roles.Add(role);
            await _dbContext.SaveChangesAsync();

            // Log role creation
            var permissionNames = string.Join(", ", permissions.Select(p => p.ToString()));
            await _auditLogService.LogAsync(
                "ROLE_CREATED",
                "Role",
                role.Id,
                $"Role '{role.Name}' created with permissions: {permissionNames}");

            _logger.LogInformation(
                "Role {RoleId} ({RoleName}) created by admin {AdminId}. Permissions: {Permissions}", 
                role.Id, role.Name, createdBy, permissionNames);

            TempData["Success"] = $"Role '{role.Name}' created successfully.";
            return RedirectToPage("/Roles/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create role {RoleName}", Name);
            ModelState.AddModelError(string.Empty, "An error occurred while creating the role. Please try again.");
            return Page();
        }
    }

    private void LoadPermissions()
    {
        var allPermissions = Enum.GetValues<Permission>()
            .Where(p => p != Permission.None)
            .Select(p => new PermissionItem(p.ToString(), GetPermissionDescription(p)))
            .ToList();

        // Group permissions by module
        PermissionGroups = allPermissions
            .GroupBy(p => GetPermissionModule(p.Name))
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private static string GetPermissionModule(string permissionName)
    {
        // Extract module from permission name (e.g., "DocumentRead" -> "Document")
        var modules = new[] { "Document", "Task", "Audit", "Capa", "User", "System" };
        foreach (var module in modules)
        {
            if (permissionName.StartsWith(module, StringComparison.OrdinalIgnoreCase))
                return module;
        }
        return "Other";
    }

    private static string GetPermissionDescription(Permission permission)
    {
        return permission switch
        {
            Permission.DocumentRead => "View documents",
            Permission.DocumentCreate => "Create new documents",
            Permission.DocumentEdit => "Edit document content",
            Permission.DocumentDelete => "Delete documents",
            Permission.DocumentApprove => "Approve documents",
            Permission.DocumentArchive => "Archive documents",
            Permission.TaskRead => "View tasks",
            Permission.TaskCreate => "Create new tasks",
            Permission.TaskEdit => "Edit task details",
            Permission.TaskDelete => "Delete tasks",
            Permission.TaskAssign => "Assign tasks to users",
            Permission.AuditRead => "View audits",
            Permission.AuditCreate => "Create new audits",
            Permission.AuditEdit => "Edit audit details",
            Permission.AuditDelete => "Delete audits",
            Permission.CapaRead => "View CAPAs",
            Permission.CapaCreate => "Create new CAPAs",
            Permission.CapaEdit => "Edit CAPA details",
            Permission.CapaDelete => "Delete CAPAs",
            Permission.CapaVerify => "Verify CAPA completion",
            Permission.UserRead => "View users",
            Permission.UserCreate => "Create new users",
            Permission.UserEdit => "Edit user details",
            Permission.UserDelete => "Delete users",
            Permission.SystemSettings => "Manage system settings",
            Permission.ViewAuditLogs => "View audit logs",
            Permission.ManageRoles => "Manage roles and permissions",
            _ => permission.ToString()
        };
    }

    public record PermissionItem(string Name, string Description);
}
