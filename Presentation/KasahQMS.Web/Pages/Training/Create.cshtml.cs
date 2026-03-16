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

    public async Task OnGetAsync()
    {
        await LoadUsersAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
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

        Users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new UserOption(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();
    }

    public record UserOption(Guid Id, string Name);
}
