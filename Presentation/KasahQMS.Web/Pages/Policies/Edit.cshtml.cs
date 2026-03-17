using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Policies;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public EditModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [BindProperty] public Guid Id { get; set; }
    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public Guid? RoleId { get; set; }
    [BindProperty] public string Scope { get; set; } = string.Empty;
    [BindProperty] public string Attribute { get; set; } = string.Empty;
    [BindProperty] public string Operator { get; set; } = "Equals";
    [BindProperty] public string Value { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }

    public List<RoleItem> Roles { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var userId = _currentUserService.UserId;

        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Check permission
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
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId && p.IsActive);

        if (policy == null)
            return NotFound();

        Id = policy.Id;
        Name = policy.Name;
        RoleId = policy.RoleId;
        Scope = policy.Scope;
        Attribute = policy.Attribute;
        Operator = policy.Operator;
        Value = policy.Value;
        Description = policy.Description;

        await LoadRolesAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var userId = _currentUserService.UserId;

        if (!userId.HasValue)
            return RedirectToPage("/Account/Login");

        // Check permission
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

        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Scope) ||
            string.IsNullOrWhiteSpace(Attribute) || string.IsNullOrWhiteSpace(Value))
        {
            ModelState.AddModelError(string.Empty, "Please complete all required fields.");
            await LoadRolesAsync();
            return Page();
        }

        var policy = await _dbContext.AccessPolicies
            .FirstOrDefaultAsync(p => p.Id == Id && p.TenantId == tenantId);

        if (policy == null)
            return NotFound();

        policy.Name = Name;
        policy.RoleId = RoleId;
        policy.Scope = Scope;
        policy.Attribute = Attribute;
        policy.Operator = Operator;
        policy.Value = Value;
        policy.Description = Description;
        policy.LastModifiedById = userId;
        policy.LastModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return RedirectToPage("./Index");
    }

    private async Task LoadRolesAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        Roles = await _dbContext.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .Select(r => new RoleItem(r.Id, r.Name))
            .ToListAsync();
    }

    public record RoleItem(Guid Id, string Name);
}
