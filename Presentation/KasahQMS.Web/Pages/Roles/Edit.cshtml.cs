using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Roles;

public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;

    public EditModel(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty] public string Name { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public List<string> SelectedPermissions { get; set; } = new();

    public List<string> AllPermissions { get; set; } = Enum.GetNames<Permission>().Where(p => p != nameof(Permission.None)).ToList();

    public async Task<IActionResult> OnGetAsync()
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == Id);
        if (role == null)
        {
            return RedirectToPage("/Roles/Index");
        }

        Name = role.Name;
        Description = role.Description;
        SelectedPermissions = role.Permissions.Select(p => p.ToString()).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Id == Id);
        if (role == null)
        {
            return RedirectToPage("/Roles/Index");
        }

        role.Name = Name;
        role.Description = Description;
        role.Permissions = SelectedPermissions
            .Select(p => Enum.TryParse<Permission>(p, out var perm) ? perm : Permission.None)
            .Where(p => p != Permission.None)
            .ToArray();

        await _dbContext.SaveChangesAsync();
        return RedirectToPage("/Roles/Index");
    }
}
