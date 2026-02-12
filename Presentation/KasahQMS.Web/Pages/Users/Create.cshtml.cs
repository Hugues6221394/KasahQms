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
/// Create new user page. Only System Admin can create users.
/// Per QMS requirements, System Admin is responsible for:
/// - Creating user accounts
/// - Assigning roles
/// - Assigning organization units (departments)
/// - Setting up reporting hierarchy (ManagerId)
/// </summary>
[Authorize(Roles = "System Admin,SystemAdmin,Admin,TenantAdmin")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext, 
        IPasswordHasher passwordHasher, 
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [BindProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "First name is required")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Last name is required")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    public string? JobTitle { get; set; }

    [BindProperty]
    public string? PhoneNumber { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string Password { get; set; } = "P@ssw0rd!";

    [BindProperty]
    [Required(ErrorMessage = "Organization unit (department) is required")]
    public Guid? OrganizationUnitId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Manager is required for hierarchy")]
    public Guid? ManagerId { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "At least one role must be selected")]
    public List<Guid> SelectedRoleIds { get; set; } = new();

    public List<LookupItem> Roles { get; set; } = new();
    public List<LookupItem> OrganizationUnits { get; set; } = new();
    public List<ManagerLookupItem> Managers { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();

        // Validate email uniqueness
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        
        var emailExists = await _dbContext.Users.AnyAsync(u => 
            u.TenantId == tenantId && 
            u.Email.ToLower() == Email.ToLower());

        if (emailExists)
        {
            ModelState.AddModelError(nameof(Email), "A user with this email already exists.");
            return Page();
        }

        // Validate role selection
        if (!SelectedRoleIds.Any())
        {
            ModelState.AddModelError(nameof(SelectedRoleIds), "At least one role must be selected.");
            return Page();
        }

        // Validate manager exists and is not a circular reference
        if (ManagerId.HasValue)
        {
            var manager = await _dbContext.Users.FindAsync(ManagerId.Value);
            if (manager == null)
            {
                ModelState.AddModelError(nameof(ManagerId), "Selected manager does not exist.");
                return Page();
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var createdBy = _currentUserService.UserId ?? Guid.Empty;

        try
        {
            var hashed = _passwordHasher.Hash(Password);
            var user = KasahQMS.Domain.Entities.Identity.User.Create(
                tenantId, Email, FirstName, LastName, hashed, createdBy);
            
            user.JobTitle = JobTitle;
            user.PhoneNumber = PhoneNumber;
            user.RequirePasswordChange = true; // Force password change on first login

            if (OrganizationUnitId.HasValue)
            {
                user.AssignToOrganizationUnit(OrganizationUnitId.Value);
            }

            if (ManagerId.HasValue)
            {
                user.SetManager(ManagerId.Value);
            }

            var roles = await _dbContext.Roles
                .Where(r => SelectedRoleIds.Contains(r.Id))
                .ToListAsync();
            user.Roles = roles;

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            // Log user creation
            var roleNames = string.Join(", ", roles.Select(r => r.Name));
            await _auditLogService.LogAsync(
                "USER_CREATED",
                "User",
                user.Id,
                $"User '{user.FullName}' ({user.Email}) created with roles: {roleNames}");

            _logger.LogInformation(
                "User {UserId} created by admin {AdminId}. Roles: {Roles}", 
                user.Id, createdBy, roleNames);

            TempData["Success"] = $"User '{user.FullName}' created successfully. They must change their password on first login.";

            return RedirectToPage("/Users/Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user {Email}", Email);
            ModelState.AddModelError(string.Empty, "An error occurred while creating the user. Please try again.");
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

        // Load potential managers (users who can have subordinates)
        // Typically managers are users with manager roles
        Managers = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
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
