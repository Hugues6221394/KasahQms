using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Policies;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<PolicyRow> Policies { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        Policies = await _dbContext.AccessPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Role)
            .OrderBy(p => p.Name)
            .Select(p => new PolicyRow(
                p.Name,
                p.Description ?? string.Empty,
                p.Role != null ? p.Role.Name : "All roles",
                $"{p.Attribute} {p.Operator} {p.Value}",
                p.IsActive ? "Active" : "Inactive"))
            .ToListAsync();
    }

    public record PolicyRow(string Name, string Description, string Role, string Rule, string Status);
}

