using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Training;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Training;

[Authorize]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<CreateModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string TrainingType { get; set; } = "Initial";
    [BindProperty] public Guid UserId { get; set; }
    [BindProperty] public DateTime ScheduledDate { get; set; } = DateTime.UtcNow.Date;
    [BindProperty] public Guid? TrainerId { get; set; }
    [BindProperty] public int? PassingScore { get; set; }

    public List<UserOption> Users { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CanCreateTrainingAsync())
            return RedirectToPage("/Account/AccessDenied");
            
        await LoadUsersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await CanCreateTrainingAsync())
            return RedirectToPage("/Account/AccessDenied");
            
        if (string.IsNullOrWhiteSpace(Title))
            ModelState.AddModelError(nameof(Title), "Title is required.");

        if (UserId == Guid.Empty)
            ModelState.AddModelError(nameof(UserId), "Employee is required.");

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync();
            return Page();
        }

        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        // Authorization: Ensure user can only create trainings for allowed users
        if (!await CanCreateForUserAsync(UserId))
        {
            ErrorMessage = "You are not authorized to create training for this user.";
            await LoadUsersAsync();
            return Page();
        }

        if (!Enum.TryParse<Domain.Enums.TrainingType>(TrainingType, out var tt))
            tt = Domain.Enums.TrainingType.Initial;

        var record = new TrainingRecord
        {
            Id = Guid.NewGuid(),
            Title = Title,
            Description = Description,
            TrainingType = tt,
            UserId = UserId,
            ScheduledDate = ScheduledDate,
            TrainerId = TrainerId,
            PassingScore = PassingScore,
            Status = TrainingStatus.Scheduled,
            TenantId = tenantId,
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.TrainingRecords.Add(record);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Training record {Id} created by {UserId}", record.Id, currentUserId);
        return RedirectToPage("./Index");
    }

    private async Task LoadUsersAsync()
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId;

        if (currentUserId == null)
        {
            Users = new();
            return;
        }

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
        {
            Users = new();
            return;
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrDeputy = roles.Any(r =>
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase));

        var isManager = !isTmdOrDeputy && roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));

        var query = _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive);

        // TMD/Deputy: can create for all users
        // Manager: can only create for their direct reports
        // Staff: cannot create (blocked at OnGetAsync)
        if (!isTmdOrDeputy && isManager)
        {
            // Get manager's subordinates
            var subordinateIds = await _dbContext.Users
                .Where(u => u.ManagerId == currentUserId && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();
            
            query = query.Where(u => subordinateIds.Contains(u.Id));
        }

        Users = await query
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new UserOption(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();
    }

    /// <summary>
    /// Check if current user can create trainings.
    /// Staff: cannot create trainings (only view assigned)
    /// Managers: can create for their subordinates
    /// TMD/Deputy: can create for anyone
    /// </summary>
    private async Task<bool> CanCreateTrainingAsync()
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null) return false;

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();

        // TMD/Deputy can create for everyone
        var isTmdOrDeputy = roles.Any(r =>
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase));

        if (isTmdOrDeputy) return true;

        // Manager can create for subordinates
        var isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));
        if (isManager)
        {
            // Check if they have any subordinates
            var hasSubordinates = await _dbContext.Users.AnyAsync(u => u.ManagerId == currentUserId && u.IsActive);
            return hasSubordinates;
        }

        // Staff cannot create
        return false;
    }

    /// <summary>
    /// Check if current user can create training for specific target user.
    /// </summary>
    private async Task<bool> CanCreateForUserAsync(Guid targetUserId)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null) return false;

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();

        // TMD/Deputy can create for anyone
        var isTmdOrDeputy = roles.Any(r =>
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase));

        if (isTmdOrDeputy) return true;

        // Manager can only create for their direct reports
        var isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));
        if (isManager)
        {
            var isSubordinate = await _dbContext.Users
                .AnyAsync(u => u.Id == targetUserId && u.ManagerId == currentUserId && u.IsActive);
            return isSubordinate;
        }

        return false;
    }

    public record UserOption(Guid Id, string Name);
}
