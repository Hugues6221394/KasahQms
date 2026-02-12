using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Policies;

public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public CreateModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public Guid? RoleId { get; set; }
    [BindProperty] public string Scope { get; set; } = string.Empty;
    [BindProperty] public string Attribute { get; set; } = string.Empty;
    [BindProperty] public string Operator { get; set; } = "Equals";
    [BindProperty] public string Value { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }

    public List<RoleItem> Roles { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadRolesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRolesAsync();

        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Scope) ||
            string.IsNullOrWhiteSpace(Attribute) || string.IsNullOrWhiteSpace(Value))
        {
            ModelState.AddModelError(string.Empty, "Please complete all required fields.");
            return Page();
        }

        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var createdBy = _currentUserService.UserId ?? Guid.Empty;

        var policy = KasahQMS.Domain.Entities.Identity.AccessPolicy.Create(
            tenantId,
            Name,
            Scope,
            Attribute,
            Operator,
            Value,
            createdBy,
            RoleId,
            Description);

        _dbContext.AccessPolicies.Add(policy);
        await _dbContext.SaveChangesAsync();

        return RedirectToPage("/Policies/Index");
    }

    private async Task LoadRolesAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        Roles = await _dbContext.Roles.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .Select(r => new RoleItem(r.Id, r.Name))
            .ToListAsync();
    }

    public record RoleItem(Guid Id, string Name);
}

