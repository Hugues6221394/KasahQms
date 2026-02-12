using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Roles;

/// <summary>
/// Role management page. Only System Admin can manage roles.
/// Per QMS requirements, System Admin defines:
/// - Roles and their names
/// - Base permissions per role
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

    public List<RoleCard> Roles { get; set; } = new();
    public int TotalRoles { get; set; }
    public int SystemRoles { get; set; }
    public int CustomRoles { get; set; }

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        
        var policies = await _dbContext.AccessPolicies
            .Where(p => p.TenantId == tenantId)
            .AsNoTracking()
            .ToListAsync();

        var roles = await _dbContext.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.IsSystemRole ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync();

        // Get user counts per role
        var userRoleCounts = await _dbContext.UserRoles
            .Where(ur => roles.Select(r => r.Id).Contains(ur.RoleId))
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count);

        Roles = roles.Select(r => new RoleCard(
            r.Id,
            r.Name,
            r.Description ?? "Role permissions and policy constraints.",
            r.IsSystemRole,
            r.Permissions.Select(p => p.ToString()).ToList(),
            policies.Where(p => p.RoleId == r.Id)
                .Select(p => new PolicyItem(p.Name, $"{p.Attribute} {p.Operator} {p.Value}"))
                .ToList(),
            userRoleCounts.GetValueOrDefault(r.Id, 0)))
        .ToList();

        TotalRoles = Roles.Count;
        SystemRoles = Roles.Count(r => r.IsSystemRole);
        CustomRoles = TotalRoles - SystemRoles;

        _logger.LogInformation("Role list accessed by admin {UserId}. Showing {Count} roles.", 
            _currentUserService.UserId, Roles.Count);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var role = await _dbContext.Roles.FindAsync(id);
        if (role == null)
        {
            TempData["Error"] = "Role not found.";
            return RedirectToPage();
        }

        // Prevent deletion of system roles
        if (role.IsSystemRole)
        {
            TempData["Error"] = "Cannot delete system roles.";
            return RedirectToPage();
        }

        // Check if role is assigned to any users
        var usersWithRole = await _dbContext.UserRoles.AnyAsync(ur => ur.RoleId == id);
        if (usersWithRole)
        {
            TempData["Error"] = "Cannot delete role that is assigned to users. Remove the role from all users first.";
            return RedirectToPage();
        }

        // Remove associated policies
        var policies = await _dbContext.AccessPolicies.Where(p => p.RoleId == id).ToListAsync();
        _dbContext.AccessPolicies.RemoveRange(policies);

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync();

        await _auditLogService.LogAsync(
            "ROLE_DELETED",
            "Role",
            role.Id,
            $"Role '{role.Name}' deleted by admin");

        _logger.LogInformation("Role {RoleId} ({RoleName}) deleted by admin {AdminId}", 
            id, role.Name, _currentUserService.UserId);
        
        TempData["Success"] = $"Role '{role.Name}' deleted successfully.";
        return RedirectToPage();
    }

    public record RoleCard(
        Guid Id, 
        string Name, 
        string Description, 
        bool IsSystemRole,
        IEnumerable<string> Permissions, 
        IEnumerable<PolicyItem> Policies,
        int UserCount);
    public record PolicyItem(string Name, string Rule);
}


