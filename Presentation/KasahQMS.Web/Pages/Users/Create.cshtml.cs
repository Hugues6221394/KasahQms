using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace KasahQMS.Web.Pages.Users;

/// <summary>
/// Create new user page. System Admin, TMD, and Deputy can create users.
/// </summary>
[Authorize(Roles = "System Admin,SystemAdmin,Admin,TenantAdmin,Tenant Admin,TMD,Deputy,TenantMD,Deputy Director")]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IEmailService _emailService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext, 
        IPasswordHasher passwordHasher, 
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IEmailService emailService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _emailService = emailService;
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

    /// <summary>
    /// If true, skip manual password and send a reset link to user's email.
    /// </summary>
    [BindProperty]
    public bool SendResetLink { get; set; } = false;

    // Nullable so ASP.NET Core doesn't implicitly treat it as [Required]
    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public Guid? OrganizationUnitId { get; set; }

    // ManagerId is optional - TMD/top-level users have no manager
    [BindProperty]
    public Guid? ManagerId { get; set; }

    [BindProperty]
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

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // If sending reset link, password is not needed — clear any implicit required error
        if (SendResetLink)
        {
            ModelState.Remove(nameof(Password));
        }

        // Manual password required only if not sending reset link
        if (!SendResetLink && string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(nameof(Password), "Password is required when not sending a reset link.");
        }
        if (!SendResetLink && !string.IsNullOrWhiteSpace(Password) && Password.Length < 8)
        {
            ModelState.AddModelError(nameof(Password), "Password must be at least 8 characters.");
        }

        // Validate email uniqueness
        var emailExists = await _dbContext.Users.AnyAsync(u => 
            u.TenantId == tenantId && 
            u.Email.ToLower() == Email.ToLower());

        if (emailExists)
        {
            ModelState.AddModelError(nameof(Email), "A user with this email already exists.");
        }

        // Validate role selection
        if (!SelectedRoleIds.Any())
        {
            ModelState.AddModelError(nameof(SelectedRoleIds), "At least one role must be selected.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var createdBy = _currentUserService.UserId ?? Guid.Empty;

        try
        {
            // Use a placeholder hash if we're sending a reset link; user sets real password via the link
            var passwordHash = SendResetLink
                ? _passwordHasher.Hash(Guid.NewGuid().ToString()) // random unreachable password
                : _passwordHasher.Hash(Password);

            var user = KasahQMS.Domain.Entities.Identity.User.Create(
                tenantId, Email, FirstName, LastName, passwordHash, createdBy);
            
            user.JobTitle = JobTitle;
            user.PhoneNumber = PhoneNumber;
            user.RequirePasswordChange = true; // Always force password change on first login

            if (OrganizationUnitId.HasValue)
                user.AssignToOrganizationUnit(OrganizationUnitId.Value);

            if (ManagerId.HasValue)
                user.SetManager(ManagerId.Value);

            // Assign roles via explicit join entities (correct many-to-many with explicit join table)
            var roleIds = SelectedRoleIds.ToHashSet();
            var roles = await _dbContext.Roles
                .Where(r => roleIds.Contains(r.Id))
                .ToListAsync();

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(); // Save first to get user.Id

            // Now add UserRole join records
            foreach (var role in roles)
            {
                _dbContext.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id,
                    AssignedAt = DateTimeOffset.UtcNow,
                    AssignedBy = createdBy
                });
            }
            await _dbContext.SaveChangesAsync();

            var roleNames = string.Join(", ", roles.Select(r => r.Name));

            // Send welcome or reset link email
            if (SendResetLink)
            {
                // Generate DB-persisted token (survives server restarts)
                var rawBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                var token = Convert.ToBase64String(rawBytes)
                    .Replace("+", "-").Replace("/", "_").Replace("=", "");
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(24);
                await _dbContext.SaveChangesAsync(); // persist token

                var resetLink = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(user.Email)}";
                await _emailService.SendEmailAsync(
                    user.Email,
                    "Welcome to KASAH QMS – Set Your Password",
                    $"<p>Dear {user.FirstName},</p>" +
                    $"<p>Your account has been created in <strong>KASAH QMS</strong>.</p>" +
                    $"<p>Click the link below to set your password and get started:</p>" +
                    $"<p><a href=\"{resetLink}\" style=\"background:#0c88e8;color:white;padding:10px 22px;border-radius:5px;text-decoration:none;\">Set My Password</a></p>" +
                    $"<p>This link expires in 24 hours.</p>" +
                    $"<p>— KASAH QMS Team</p>",
                    isHtml: true);
            }
            else
            {
                await _emailService.SendWelcomeEmailAsync(
                    user.Email, user.FullName, user.Email, Password);
            }

            await _auditLogService.LogAsync(
                "USER_CREATED",
                "User",
                user.Id,
                $"User '{user.FullName}' ({user.Email}) created with roles: {roleNames}");

            _logger.LogInformation("User {UserId} created by {AdminId}. Roles: {Roles}", user.Id, createdBy, roleNames);

            TempData["Success"] = SendResetLink
                ? $"User '{user.FullName}' created. A password-setup link was sent to {user.Email}."
                : $"User '{user.FullName}' created successfully. Share the temporary password with them.";

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

        Managers = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new ManagerLookupItem(u.Id, u.FullName, u.JobTitle ?? ""))
            .ToListAsync();
    }

    public record LookupItem(Guid Id, string Name, string? Description = null);
    public record ManagerLookupItem(Guid Id, string Name, string Role);
}
