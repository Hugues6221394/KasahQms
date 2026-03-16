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
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public RiskDetailView? Risk { get; set; }
    public List<ActionRow> Actions { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }

    [BindProperty] public string? NewAction { get; set; }
    [BindProperty] public Guid? NewActionOwnerId { get; set; }
    [BindProperty] public DateTime? NewActionDueDate { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        return await LoadRiskAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostTransitionAsync(Guid id, string newStatus)
    {
        var risk = await GetRiskEntity(id);
        if (risk == null) return NotFound();

        if (Enum.TryParse<RiskStatus>(newStatus, out var status))
        {
            risk.Status = status;
            risk.LastModifiedById = _currentUserService.UserId;
            risk.LastModifiedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
            return RedirectToPage(new { id, message = $"Status changed to {status}.", success = true });
        }

        return RedirectToPage(new { id, message = "Invalid status.", success = false });
    }

    public async Task<IActionResult> OnPostAddActionAsync(Guid id)
    {
        if (string.IsNullOrWhiteSpace(NewAction))
            return RedirectToPage(new { id, message = "Action description is required.", success = false });

        var risk = await GetRiskEntity(id);
        if (risk == null) return NotFound();

        var entry = new RiskRegisterEntry
        {
            Id = Guid.NewGuid(),
            RiskAssessmentId = id,
            Action = NewAction,
            ActionOwnerId = NewActionOwnerId != Guid.Empty ? NewActionOwnerId : null,
            DueDate = NewActionDueDate,
            Status = "Open"
        };

        _dbContext.RiskRegisterEntries.Add(entry);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Action item added to risk {RiskId} by {UserId}", id, _currentUserService.UserId);
        return RedirectToPage(new { id, message = "Action item added.", success = true });
    }

    public async Task<IActionResult> OnPostCompleteActionAsync(Guid id, Guid actionId)
    {
        var entry = await _dbContext.RiskRegisterEntries.FirstOrDefaultAsync(e => e.Id == actionId);
        if (entry == null) return NotFound();

        entry.Status = "Completed";
        entry.CompletedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Action marked as completed.", success = true });
    }

    private async Task<RiskAssessment?> GetRiskEntity(Guid id)
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        return await _dbContext.RiskAssessments
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);
    }

    private async Task<bool> LoadRiskAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var r = await _dbContext.RiskAssessments.AsNoTracking()
            .Include(x => x.Owner)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

        if (r == null) return false;

        Risk = new RiskDetailView(
            r.Id, r.RiskNumber, r.Title, r.Description,
            r.Category ?? "—", r.Likelihood, r.Impact, r.RiskScore,
            IndexModel.GetScoreBadgeClass(r.RiskScore),
            IndexModel.GetScoreLabel(r.RiskScore),
            r.Status.ToString(),
            r.Owner != null ? r.Owner.FirstName + " " + r.Owner.LastName : "—",
            r.MitigationPlan,
            r.ResidualLikelihood, r.ResidualImpact,
            r.ReviewDate?.ToString("MMM dd, yyyy"),
            r.CreatedAt.ToString("MMM dd, yyyy HH:mm"));

        Actions = await _dbContext.RiskRegisterEntries.AsNoTracking()
            .Include(e => e.ActionOwner)
            .Where(e => e.RiskAssessmentId == id)
            .OrderByDescending(e => e.DueDate)
            .Select(e => new ActionRow(
                e.Id,
                e.Action ?? "—",
                e.ActionOwner != null ? e.ActionOwner.FirstName + " " + e.ActionOwner.LastName : "—",
                e.DueDate.HasValue ? e.DueDate.Value.ToString("MMM dd, yyyy") : "—",
                e.CompletedDate.HasValue ? e.CompletedDate.Value.ToString("MMM dd, yyyy") : null,
                e.Status ?? "Open"))
            .ToListAsync();

        await LoadUsersAsync(tenantId);
        return true;
    }

    private async Task LoadUsersAsync(Guid tenantId)
    {
        Users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new UserOption(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();
    }

    public record RiskDetailView(
        Guid Id, string RiskNumber, string Title, string? Description,
        string Category, int Likelihood, int Impact, int Score,
        string ScoreClass, string ScoreLabel, string Status, string Owner,
        string? MitigationPlan, int? ResidualLikelihood, int? ResidualImpact,
        string? ReviewDate, string CreatedAt);

    public record ActionRow(Guid Id, string Action, string Owner, string DueDate, string? CompletedDate, string Status);
    public record UserOption(Guid Id, string Name);
}
