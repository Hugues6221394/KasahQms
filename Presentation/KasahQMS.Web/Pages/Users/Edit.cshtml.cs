using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Users;

/// <summary>
/// Edit user page. Only System Admin can edit users.
/// Supports modifying:
/// - Job title
/// - Organization unit (department)
/// - Manager (reporting hierarchy)
/// - Roles
/// - Active status
/// </summary>
[Authorize(Roles = "System Admin,SystemAdmin,Admin,TenantAdmin")]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IHierarchyService _hierarchyService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IHierarchyService hierarchyService,
        ILogger<EditModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    // Display only fields
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CurrentRoles { get; set; }
    public string? CurrentDepartment { get; set; }
    public string? CurrentManager { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; }

    // Editable fields
    [BindProperty]
    public string? JobTitle { get; set; }

    [BindProperty]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Organization unit (department) is required")]
    public Guid? OrganizationUnitId { get; set; }

    [BindProperty]
    public Guid? ManagerId { get; set; }

    [BindProperty]
    public bool IsActive { get; set; } = true;

    [BindProperty]
    [Required(ErrorMessage = "At least one role must be selected")]
    public List<Guid> SelectedRoleIds { get; set; } = new();

    public List<LookupItem> Roles { get; set; } = new();
    public List<LookupItem> OrganizationUnits { get; set; } = new();
    public List<ManagerLookupItem> Managers { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .Include(u => u.Manager)
            .FirstOrDefaultAsync(u => u.Id == Id);

        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToPage("/Users/Index");
        }

        // Populate display fields
        DisplayName = user.FullName;
        Email = user.Email;
        CurrentRoles = user.Roles?.Any() == true ? string.Join(", ", user.Roles.Select(r => r.Name)) : "No Role";
        CurrentDepartment = user.OrganizationUnit?.Name ?? "Unassigned";
        CurrentManager = user.Manager?.FullName ?? "No Manager";
        LastLogin = user.LastLoginAt;
        CreatedAt = user.CreatedAt;

        // Populate editable fields
        JobTitle = user.JobTitle;
        PhoneNumber = user.PhoneNumber;
        OrganizationUnitId = user.OrganizationUnitId;
        ManagerId = user.ManagerId;
        IsActive = user.IsActive;
        SelectedRoleIds = user.Roles?.Select(r => r.Id).ToList() ?? new List<Guid>();

        await LoadLookupsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == Id);

        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToPage("/Users/Index");
        }

        // Populate display fields for re-render
        DisplayName = user.FullName;
        Email = user.Email;
        LastLogin = user.LastLoginAt;
        CreatedAt = user.CreatedAt;

        // Validate role selection
        if (!SelectedRoleIds.Any())
        {
            ModelState.AddModelError(nameof(SelectedRoleIds), "At least one role must be selected.");
            return Page();
        }

        // Validate manager is not creating a circular hierarchy
        if (ManagerId.HasValue)
        {
            if (ManagerId.Value == Id)
            {
                ModelState.AddModelError(nameof(ManagerId), "A user cannot be their own manager.");
                return Page();
            }

            // Check if the selected manager is a subordinate of this user (would create circular reference)
            var subordinates = await _hierarchyService.GetSubordinateUserIdsAsync(Id);
            if (subordinates.Contains(ManagerId.Value))
            {
                ModelState.AddModelError(nameof(ManagerId), "Cannot set a subordinate as manager (circular hierarchy).");
                return Page();
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            // Track changes for audit
            var changes = new List<string>();

            if (user.JobTitle != JobTitle)
            {
                changes.Add($"Job title: '{user.JobTitle}' → '{JobTitle}'");
                user.JobTitle = JobTitle;
            }

            if (user.PhoneNumber != PhoneNumber)
            {
                changes.Add($"Phone: '{user.PhoneNumber}' → '{PhoneNumber}'");
                user.PhoneNumber = PhoneNumber;
            }

            if (user.OrganizationUnitId != OrganizationUnitId)
            {
                var oldDept = user.OrganizationUnitId.HasValue 
                    ? await _dbContext.OrganizationUnits.Where(o => o.Id == user.OrganizationUnitId.Value).Select(o => o.Name).FirstOrDefaultAsync()
                    : "None";
                var newDept = OrganizationUnitId.HasValue 
                    ? await _dbContext.OrganizationUnits.Where(o => o.Id == OrganizationUnitId.Value).Select(o => o.Name).FirstOrDefaultAsync()
                    : "None";
                changes.Add($"Department: '{oldDept}' → '{newDept}'");

                if (OrganizationUnitId.HasValue)
                    user.AssignToOrganizationUnit(OrganizationUnitId.Value);
                else
                    user.OrganizationUnitId = null;
            }

            if (user.ManagerId != ManagerId)
            {
                var oldManager = user.ManagerId.HasValue 
                    ? await _dbContext.Users.Where(u => u.Id == user.ManagerId.Value).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync()
                    : "None";
                var newManager = ManagerId.HasValue 
                    ? await _dbContext.Users.Where(u => u.Id == ManagerId.Value).Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync()
                    : "None";
                changes.Add($"Manager: '{oldManager}' → '{newManager}'");

                if (ManagerId.HasValue)
                    user.SetManager(ManagerId.Value);
                else
                    user.ManagerId = null;
            }

            if (user.IsActive != IsActive)
            {
                changes.Add($"Status: {(user.IsActive ? "Active" : "Inactive")} → {(IsActive ? "Active" : "Inactive")}");
                if (IsActive)
                    user.Activate();
                else
                    user.Deactivate();
            }

            // Update roles
            var newRoles = await _dbContext.Roles
                .Where(r => SelectedRoleIds.Contains(r.Id))
                .ToListAsync();

            var oldRoleNames = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
            var newRoleNames = newRoles.Select(r => r.Name).ToList();

            if (!oldRoleNames.OrderBy(x => x).SequenceEqual(newRoleNames.OrderBy(x => x)))
            {
                changes.Add($"Roles: [{string.Join(", ", oldRoleNames)}] → [{string.Join(", ", newRoleNames)}]");
                user.Roles = newRoles;
            }

            await _dbContext.SaveChangesAsync();

            // Log changes if any
            if (changes.Any())
            {
                await _auditLogService.LogAsync(
                    "USER_UPDATED",
                    "User",
                    user.Id,
                    $"User '{user.FullName}' updated. Changes: {string.Join("; ", changes)}");

                _logger.LogInformation(
                    "User {UserId} updated by admin {AdminId}. Changes: {Changes}", 
                    user.Id, _currentUserService.UserId, string.Join("; ", changes));
            }

            TempData["Success"] = $"User '{user.FullName}' updated successfully.";
            return RedirectToPage("/Users/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user {UserId}", Id);
            ModelState.AddModelError(string.Empty, "An error occurred while updating the user. Please try again.");
            return Page();
        }
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        Roles = await _dbContext.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .Select(r => new LookupItem(r.Id, r.Name, r.Description))
            .ToListAsync();

        OrganizationUnits = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(o => o.TenantId == tenantId && o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new LookupItem(o.Id, o.Name, o.Description))
            .ToListAsync();

        // Load potential managers (exclude current user to prevent self-assignment)
        Managers = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive && u.Id != Id)
            .Include(u => u.Roles)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new ManagerLookupItem(
                u.Id, 
                u.FullName, 
                u.Roles != null && u.Roles.Any() ? string.Join(", ", u.Roles.Select(r => r.Name)) : "No Role"))
            .ToListAsync();
    }

    public record LookupItem(Guid Id, string Name, string? Description = null);
    public record ManagerLookupItem(Guid Id, string Name, string Role);
}
