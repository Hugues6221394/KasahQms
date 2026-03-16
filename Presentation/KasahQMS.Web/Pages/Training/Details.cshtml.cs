using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Training;

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

    public TrainingDetailView? Record { get; set; }
    public List<CompetencyRow> Competencies { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }

    [BindProperty] public int? CompletionScore { get; set; }
    [BindProperty] public string? CertificateNumber { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        return await LoadRecordAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostStartAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        record.Status = TrainingStatus.InProgress;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Training marked as In Progress.", success = true });
    }

    public async Task<IActionResult> OnPostCompleteAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        record.Status = TrainingStatus.Completed;
        record.CompletedDate = DateTime.UtcNow;
        record.Score = CompletionScore;
        record.CertificateNumber = CertificateNumber;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Training completed.", success = true });
    }

    public async Task<IActionResult> OnPostExpireAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        record.Status = TrainingStatus.Expired;
        record.ExpiryDate = DateTime.UtcNow;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Training marked as Expired.", success = true });
    }

    public async Task<IActionResult> OnPostUpdateCertificateAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        record.CertificateNumber = CertificateNumber;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Certificate number updated.", success = true });
    }

    private async Task<Domain.Entities.Training.TrainingRecord?> GetRecordEntity(Guid id)
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        return await _dbContext.TrainingRecords
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);
    }

    private async Task<bool> LoadRecordAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var t = await _dbContext.TrainingRecords.AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Trainer)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId);

        if (t == null) return false;

        Record = new TrainingDetailView(
            t.Id,
            t.Title,
            t.Description,
            t.TrainingType.ToString(),
            t.Status.ToString(),
            GetStatusBadgeClass(t.Status),
            t.User != null ? t.User.FirstName + " " + t.User.LastName : "—",
            t.Trainer != null ? t.Trainer.FirstName + " " + t.Trainer.LastName : "—",
            t.ScheduledDate.ToString("MMM dd, yyyy"),
            t.CompletedDate?.ToString("MMM dd, yyyy"),
            t.ExpiryDate?.ToString("MMM dd, yyyy"),
            t.Score,
            t.PassingScore,
            t.CertificateNumber,
            t.Notes,
            t.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
            t.UserId);

        // Load competency assessments for this user
        Competencies = await _dbContext.CompetencyAssessments.AsNoTracking()
            .Include(c => c.Assessor)
            .Where(c => c.UserId == t.UserId && c.TenantId == tenantId)
            .OrderByDescending(c => c.AssessedAt)
            .Select(c => new CompetencyRow(
                c.Id,
                c.CompetencyArea,
                c.Level.ToString(),
                (int)c.Level,
                c.Assessor != null ? c.Assessor.FirstName + " " + c.Assessor.LastName : "—",
                c.AssessedAt.ToString("MMM dd, yyyy"),
                c.NextAssessmentDate.HasValue ? c.NextAssessmentDate.Value.ToString("MMM dd, yyyy") : "—"))
            .ToListAsync();

        return true;
    }

    private static string GetStatusBadgeClass(TrainingStatus s) => s switch
    {
        TrainingStatus.Completed => "bg-emerald-100 text-emerald-700",
        TrainingStatus.InProgress => "bg-brand-100 text-brand-700",
        TrainingStatus.Expired => "bg-rose-100 text-rose-700",
        _ => "bg-amber-100 text-amber-700"
    };

    public record TrainingDetailView(
        Guid Id, string Title, string? Description, string Type, string Status,
        string StatusClass, string Employee, string Trainer, string ScheduledDate,
        string? CompletedDate, string? ExpiryDate, int? Score, int? PassingScore,
        string? CertificateNumber, string? Notes, string CreatedAt, Guid UserId);

    public record CompetencyRow(
        Guid Id, string CompetencyArea, string Level, int LevelValue,
        string Assessor, string AssessedDate, string NextAssessment);
}
