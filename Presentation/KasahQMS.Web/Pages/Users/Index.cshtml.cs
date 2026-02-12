using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Users;

/// <summary>
/// User management page. Only System Admin can access this page.
/// Per QMS requirements, System Admin is responsible for:
/// - Creating user accounts
/// - Assigning roles
/// - Assigning organization units
/// - Setting up reporting hierarchy (ManagerId)
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

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? DepartmentFilter { get; set; }

    public List<UserRow> Users { get; set; } = new();
    public List<LookupItem> Departments { get; set; } = new();
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public bool IsSystemAdmin { get; set; } = true; // This page requires admin access

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Load departments for filter
        Departments = await _dbContext.OrganizationUnits.AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Name)
            .Select(o => new LookupItem(o.Id, o.Name))
            .ToListAsync();

        var query = _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .Include(u => u.Manager)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(u => 
                u.FirstName.Contains(SearchTerm) || 
                u.LastName.Contains(SearchTerm) || 
                u.Email.Contains(SearchTerm));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            bool isActive = StatusFilter == "Active";
            query = query.Where(u => u.IsActive == isActive);
        }

        // Apply department filter
        if (DepartmentFilter.HasValue)
        {
            query = query.Where(u => u.OrganizationUnitId == DepartmentFilter.Value);
        }

        // Get counts
        TotalUsers = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId);
        ActiveUsers = await _dbContext.Users.CountAsync(u => u.TenantId == tenantId && u.IsActive);
        InactiveUsers = TotalUsers - ActiveUsers;

        Users = await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Select(u => new UserRow(
                u.Id,
                u.FullName,
                u.Email,
                u.Roles != null && u.Roles.Any() ? string.Join(", ", u.Roles.Select(r => r.Name)) : "No Role",
                u.OrganizationUnit != null ? u.OrganizationUnit.Name : "Unassigned",
                u.Manager != null ? u.Manager.FullName : "No Manager",
                u.IsActive ? "Active" : "Inactive",
                u.IsActive,
                u.LastLoginAt,
                u.RequirePasswordChange))
            .ToListAsync();

        _logger.LogInformation("User list accessed by admin {UserId}. Showing {Count} users.", 
            _currentUserService.UserId, Users.Count);
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent self-deactivation
        if (currentUserId == id)
        {
            TempData["Error"] = "You cannot deactivate your own account.";
            return RedirectToPage();
        }

        // Soft delete (Deactivate)
        user.Deactivate();

        // Log the action
        await _auditLogService.LogAsync(
            "USER_DEACTIVATED",
            "User",
            user.Id,
            $"User '{user.FullName}' ({user.Email}) deactivated by admin");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} deactivated by admin {AdminId}", id, currentUserId);
        TempData["Success"] = $"User '{user.FullName}' has been deactivated.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Activate user
        user.Activate();

        // Log the action
        await _auditLogService.LogAsync(
            "USER_ACTIVATED",
            "User",
            user.Id,
            $"User '{user.FullName}' ({user.Email}) activated by admin");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} activated by admin {AdminId}", id, currentUserId);
        TempData["Success"] = $"User '{user.FullName}' has been activated.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUnlockAsync(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Unlock user
        user.Unlock();

        // Log the action
        await _auditLogService.LogAsync(
            "USER_UNLOCKED",
            "User",
            user.Id,
            $"User '{user.FullName}' ({user.Email}) account unlocked by admin");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("User {UserId} unlocked by admin {AdminId}", id, currentUserId);
        TempData["Success"] = $"User '{user.FullName}' has been unlocked.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        
        var user = await _dbContext.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Force password change on next login
        user.RequirePasswordChange = true;

        // Log the action
        await _auditLogService.LogAsync(
            "USER_PASSWORD_RESET_REQUIRED",
            "User",
            user.Id,
            $"Password reset required for user '{user.FullName}' ({user.Email}) by admin");

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password reset required for user {UserId} by admin {AdminId}", id, currentUserId);
        TempData["Success"] = $"Password reset required for '{user.FullName}' on next login.";

        return RedirectToPage();
    }

    public record UserRow(
        Guid Id, 
        string Name, 
        string Email, 
        string Role, 
        string Department, 
        string ManagerName,
        string Status, 
        bool IsActive,
        DateTime? LastLogin,
        bool RequiresPasswordChange);

    public record LookupItem(Guid Id, string Name);
}


