using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Risk;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Risk;

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
    [BindProperty] public string Category { get; set; } = "Operational";
    [BindProperty] public int Likelihood { get; set; } = 1;
    [BindProperty] public int Impact { get; set; } = 1;
    [BindProperty] public Guid OwnerId { get; set; }
    [BindProperty] public string? MitigationPlan { get; set; }
    [BindProperty] public DateTime? ReviewDate { get; set; }

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

        if (OwnerId == Guid.Empty)
            ModelState.AddModelError(nameof(OwnerId), "Owner is required.");

        Likelihood = Math.Clamp(Likelihood, 1, 5);
        Impact = Math.Clamp(Impact, 1, 5);

        if (!ModelState.IsValid)
        {
            await LoadUsersAsync();
            return Page();
        }

        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        // Generate next risk number
        var lastNumber = await _dbContext.RiskAssessments.AsNoTracking()
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.RiskNumber)
            .Select(r => r.RiskNumber)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (lastNumber != null && lastNumber.StartsWith("RSK-") && int.TryParse(lastNumber[4..], out var n))
            seq = n + 1;

        var risk = new RiskAssessment
        {
            Id = Guid.NewGuid(),
            Title = Title,
            Description = Description,
            RiskNumber = $"RSK-{seq:D4}",
            Category = Category,
            Likelihood = Likelihood,
            Impact = Impact,
            Status = RiskStatus.Identified,
            OwnerId = OwnerId,
            MitigationPlan = MitigationPlan,
            ReviewDate = ReviewDate,
            TenantId = tenantId,
            CreatedById = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RiskAssessments.Add(risk);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Risk assessment {Id} ({RiskNumber}) created by {UserId}", risk.Id, risk.RiskNumber, currentUserId);
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
