using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Policies;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<PolicyRow> Policies { get; set; } = new();
    public bool CanEdit { get; set; }

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var userId = _currentUserService.UserId;

        // Determine if user can edit policies
        if (userId.HasValue)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user != null)
            {
                var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
                bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
                bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
                CanEdit = isTmd || isAdmin;
            }
        }

        Policies = await _dbContext.AccessPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .Include(p => p.Role)
            .OrderBy(p => p.Name)
            .Select(p => new PolicyRow(
                p.Id,
                p.Name,
                p.Description ?? string.Empty,
                p.Role != null ? p.Role.Name : "All roles",
                $"{p.Attribute} {p.Operator} {p.Value}",
                p.IsActive ? "Active" : "Inactive"))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var userId = _currentUserService.UserId;

        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        bool isTmd = roles.Any(r => r == "TMD" || r == "TopManagingDirector" || r == "Country Manager");
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");

        if (!isTmd && !isAdmin)
            return RedirectToPage("/Account/AccessDenied");

        var policy = await _dbContext.AccessPolicies
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (policy != null)
        {
            policy.IsActive = false;
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public record PolicyRow(Guid Id, string Name, string Description, string Role, string Rule, string Status);
}

