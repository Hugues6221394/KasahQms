using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Profile;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public string DisplayName { get; set; } = "User";
    public string Email { get; set; } = "user@kasah.com";
    public string Role { get; set; } = "Member";

    public async Task OnGetAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return;
        }

        DisplayName = user.FullName;
        Email = user.Email;
        Role = user.Roles != null && user.Roles.Any()
            ? string.Join(", ", user.Roles.Select(r => r.Name))
            : "Member";
    }

    private async Task<KasahQMS.Domain.Entities.Identity.User?> GetCurrentUserAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        }

        return await _dbContext.Users
            .Include(u => u.Roles)
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync();
    }
}

