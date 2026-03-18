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
public class CompetenciesModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CompetenciesModel> _logger;

    public CompetenciesModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<CompetenciesModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public List<CompetencyRow> Assessments { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }

    // New assessment form
    [BindProperty] public Guid NewUserId { get; set; }
    [BindProperty] public string NewCompetencyArea { get; set; } = string.Empty;
    [BindProperty] public string NewLevel { get; set; } = "Novice";
    [BindProperty] public DateTime? NewNextAssessmentDate { get; set; }
    [BindProperty] public string? NewNotes { get; set; }

    public async Task OnGetAsync(string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
            return RedirectToPage("/Account/Login");

        if (NewUserId == Guid.Empty)
            ModelState.AddModelError(nameof(NewUserId), "Employee is required.");
        if (string.IsNullOrWhiteSpace(NewCompetencyArea))
            ModelState.AddModelError(nameof(NewCompetencyArea), "Competency area is required.");

        if (!ModelState.IsValid)
        {
            await LoadDataAsync();
            return Page();
        }

        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        DateTime? nextAssessmentDateUtc = NewNextAssessmentDate.HasValue
            ? (NewNextAssessmentDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(NewNextAssessmentDate.Value, DateTimeKind.Utc)
                : NewNextAssessmentDate.Value.ToUniversalTime())
            : null;

        if (!Enum.TryParse<CompetencyLevel>(NewLevel, out var level))
            level = CompetencyLevel.Novice;

        var assessment = new CompetencyAssessment
        {
            Id = Guid.NewGuid(),
            UserId = NewUserId,
            AssessorId = currentUserId.Value,
            CompetencyArea = NewCompetencyArea,
            Level = level,
            AssessedAt = DateTime.UtcNow,
            NextAssessmentDate = nextAssessmentDateUtc,
            Notes = NewNotes,
            IsActive = true,
            TenantId = tenantId,
            CreatedById = currentUserId.Value,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.CompetencyAssessments.Add(assessment);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Competency assessment {Id} created by {UserId}", assessment.Id, currentUserId.Value);
        return RedirectToPage(new { message = "Assessment created.", success = true });
    }

    private async Task LoadDataAsync()
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        Assessments = await _dbContext.CompetencyAssessments.AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Assessor)
            .Where(c => c.TenantId == tenantId && c.IsActive)
            .OrderByDescending(c => c.AssessedAt)
            .Select(c => new CompetencyRow(
                c.Id,
                c.User != null ? c.User.FirstName + " " + c.User.LastName : "—",
                c.CompetencyArea,
                c.Level.ToString(),
                (int)c.Level,
                c.Assessor != null ? c.Assessor.FirstName + " " + c.Assessor.LastName : "—",
                c.AssessedAt.ToString("MMM dd, yyyy"),
                c.NextAssessmentDate.HasValue ? c.NextAssessmentDate.Value.ToString("MMM dd, yyyy") : "—"))
            .ToListAsync();

        Users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new UserOption(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();
    }

    public record CompetencyRow(
        Guid Id, string Employee, string CompetencyArea, string Level, int LevelValue,
        string Assessor, string AssessedDate, string NextAssessment);

    public record UserOption(Guid Id, string Name);
}
