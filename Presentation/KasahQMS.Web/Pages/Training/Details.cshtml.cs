using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Entities.Training;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace KasahQMS.Web.Pages.Training;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        INotificationService notificationService,
        IEmailService emailService,
        IWebHostEnvironment webHostEnvironment,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _notificationService = notificationService;
        _emailService = emailService;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    public TrainingDetailView? Record { get; set; }
    public List<CompetencyRow> Competencies { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }

    public bool CanManageScheduledCrud { get; set; }
    public bool CanStartTraining { get; set; }
    public bool CanCompleteTraining { get; set; }
    public bool CanExpireTraining { get; set; }
    public bool CanUpdateCertificate { get; set; }
    public bool CanSaveAssessment { get; set; }
    public bool CanRateTrainer { get; set; }
    public bool CanManageCertificateFile { get; set; }
    public bool CanApproveCompletion { get; set; }
    public string? TrainingChatUrl { get; set; }

    [BindProperty] public int? CompletionScore { get; set; }
    [BindProperty] public string? CertificateNumber { get; set; }

    [BindProperty] public string EditTitle { get; set; } = string.Empty;
    [BindProperty] public string? EditDescription { get; set; }
    [BindProperty] public string EditTrainingType { get; set; } = "Initial";
    [BindProperty] public Guid EditUserId { get; set; }
    [BindProperty] public DateTime EditScheduledDate { get; set; } = DateTime.UtcNow.Date;
    [BindProperty] public Guid? EditTrainerId { get; set; }
    [BindProperty] public int? EditPassingScore { get; set; }

    [BindProperty] public string AssessmentArea { get; set; } = string.Empty;
    [BindProperty] public string AssessmentLevel { get; set; } = "Competent";
    [BindProperty] public DateTime? AssessmentNextDate { get; set; }
    [BindProperty] public string? AssessmentNotes { get; set; }
    [BindProperty] public string? TrainerRemarks { get; set; }
    [BindProperty] public int? TraineeRating { get; set; }
    [BindProperty] public string? TraineeFeedback { get; set; }
    [BindProperty] public string? ApprovalComment { get; set; }
    [BindProperty] public IFormFile? CertificateFile { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;

        var recordEntity = await GetRecordEntity(id);
        if (recordEntity == null)
            return NotFound();

        if (!await CanAccessTrainingAsync(recordEntity))
            return RedirectToPage("/Account/AccessDenied");

        if (!await LoadRecordAsync(id))
            return NotFound();

        await SetCapabilitiesAndDefaultsAsync(recordEntity);
        return Page();
    }

    public async Task<IActionResult> OnPostStartAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanStart(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

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

        if (!await CanAccessTrainingAsync(record) || !CanComplete(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        record.Status = TrainingStatus.Completed;
        record.CompletedDate = DateTime.UtcNow;
        record.Score = CompletionScore;
        record.CertificateNumber = CertificateNumber;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        if (record.CreatedById != Guid.Empty && record.CreatedById != _currentUserService.UserId)
        {
            var actor = await _dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId);
            var actorName = actor?.FullName ?? "A team member";

            await _notificationService.SendAsync(
                record.CreatedById,
                "Training Completed - Approval Requested",
                $"{actorName} completed training '{record.Title}' and reported it for your approval.",
                NotificationType.System,
                record.Id,
                relatedEntityType: "Training");

            var creator = await _dbContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == record.CreatedById);
            if (!string.IsNullOrWhiteSpace(creator?.Email))
            {
                await _emailService.SendEmailAsync(
                    creator.Email!,
                    $"Training Completion Approval Needed: {record.Title}",
                    $@"<p>Hello {creator.FullName},</p>
                       <p>{actorName} has completed training <strong>{record.Title}</strong> and reported it for your approval.</p>
                       <p>Please review it in the Training Hub.</p>",
                    true);
            }
        }

        return RedirectToPage(new { id, message = "Training completed and reported for approval.", success = true });
    }

    public async Task<IActionResult> OnPostUploadCertificateAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanManageCertificateFileValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        if (CertificateFile == null || CertificateFile.Length == 0)
            return RedirectToPage(new { id, message = "Please select a certificate file.", success = false });

        var ext = Path.GetExtension(CertificateFile.FileName).ToLowerInvariant();
        var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg" };
        if (!allowed.Contains(ext))
            return RedirectToPage(new { id, message = "Certificate must be PDF or image file.", success = false });

        var meta = ParseNotes(record.Notes);
        var root = _webHostEnvironment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var relativeFolder = Path.Combine("uploads", "training-certificates", record.Id.ToString());
        var absoluteFolder = Path.Combine(root, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        if (!string.IsNullOrWhiteSpace(meta.CertificateFilePath))
        {
            var oldAbsolute = Path.Combine(root, meta.CertificateFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(oldAbsolute))
                System.IO.File.Delete(oldAbsolute);
        }

        var storageName = $"{Guid.NewGuid():N}{ext}";
        var absolutePath = Path.Combine(absoluteFolder, storageName);
        await using var stream = System.IO.File.Create(absolutePath);
        await CertificateFile.CopyToAsync(stream);

        meta.CertificateFilePath = "/" + Path.Combine(relativeFolder, storageName).Replace("\\", "/");
        meta.CertificateFileName = Path.GetFileName(CertificateFile.FileName);
        record.Notes = JsonSerializer.Serialize(meta);
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Certificate uploaded successfully.", success = true });
    }

    public async Task<IActionResult> OnPostDeleteCertificateAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanManageCertificateFileValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        var meta = ParseNotes(record.Notes);
        var root = _webHostEnvironment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!string.IsNullOrWhiteSpace(meta.CertificateFilePath))
        {
            var absolute = Path.Combine(root, meta.CertificateFilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(absolute))
                System.IO.File.Delete(absolute);
        }

        meta.CertificateFilePath = null;
        meta.CertificateFileName = null;
        record.Notes = JsonSerializer.Serialize(meta);
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Certificate removed.", success = true });
    }

    public async Task<IActionResult> OnPostExpireAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanExpire(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

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

        if (!await CanAccessTrainingAsync(record) || !CanUpdateCertificateValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        record.CertificateNumber = CertificateNumber;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Certificate number updated.", success = true });
    }

    public async Task<IActionResult> OnPostUpdateScheduledAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanManageScheduled(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        if (string.IsNullOrWhiteSpace(EditTitle))
            ModelState.AddModelError(nameof(EditTitle), "Title is required.");

        if (EditUserId == Guid.Empty)
            ModelState.AddModelError(nameof(EditUserId), "Trainee is required.");

        if (!ModelState.IsValid)
        {
            await LoadRecordAsync(id);
            await SetCapabilitiesAndDefaultsAsync(record, false);
            ActionMessage = "Please correct the validation errors.";
            ActionSuccess = false;
            return Page();
        }

        if (!Enum.TryParse<TrainingType>(EditTrainingType, true, out var parsedType))
            parsedType = TrainingType.Initial;

        var scheduledDateUtc = EditScheduledDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(EditScheduledDate, DateTimeKind.Utc)
            : EditScheduledDate.ToUniversalTime();

        record.Title = EditTitle.Trim();
        record.Description = EditDescription;
        record.TrainingType = parsedType;
        record.UserId = EditUserId;
        record.ScheduledDate = scheduledDateUtc;
        record.TrainerId = EditTrainerId;
        record.PassingScore = EditPassingScore;
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return RedirectToPage(new { id, message = "Scheduled training updated.", success = true });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanManageScheduled(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        _dbContext.TrainingRecords.Remove(record);
        await _dbContext.SaveChangesAsync();

        return RedirectToPage("./Index", new { message = "Training record deleted.", success = true });
    }

    public async Task<IActionResult> OnPostSaveAssessmentAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanSaveAssessmentValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        if (string.IsNullOrWhiteSpace(AssessmentArea))
            ModelState.AddModelError(nameof(AssessmentArea), "Assessment area is required.");

        if (!ModelState.IsValid)
        {
            await LoadRecordAsync(id);
            await SetCapabilitiesAndDefaultsAsync(record, false);
            ActionMessage = "Assessment area is required.";
            ActionSuccess = false;
            return Page();
        }

        if (!Enum.TryParse<CompetencyLevel>(AssessmentLevel, true, out var level))
            level = CompetencyLevel.Competent;

        DateTime? nextDateUtc = AssessmentNextDate.HasValue
            ? (AssessmentNextDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(AssessmentNextDate.Value, DateTimeKind.Utc)
                : AssessmentNextDate.Value.ToUniversalTime())
            : null;

        var assessorId = _currentUserService.UserId!.Value;
        var meta = ParseNotes(record.Notes);
        var assessment = new CompetencyAssessment
        {
            Id = Guid.NewGuid(),
            UserId = record.UserId,
            AssessorId = assessorId,
            CompetencyArea = AssessmentArea.Trim(),
            Level = level,
            AssessedAt = DateTime.UtcNow,
            NextAssessmentDate = nextDateUtc,
            Notes = AssessmentNotes,
            IsActive = true,
            TenantId = record.TenantId,
            CreatedById = assessorId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.CompetencyAssessments.Add(assessment);
        meta.TrainerRemarks = TrainerRemarks;
        meta.TrainerAssessmentSubmittedAt = DateTime.UtcNow;
        record.Notes = JsonSerializer.Serialize(meta);
        await _dbContext.SaveChangesAsync();

        if (record.CreatedById != Guid.Empty && record.CreatedById != assessorId)
        {
            var assessor = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == assessorId);
            var assessorName = assessor?.FullName ?? "Assessor";
            await _notificationService.SendAsync(
                record.CreatedById,
                "Training Assessment Submitted",
                $"{assessorName} submitted an assessment for training '{record.Title}'.",
                NotificationType.System,
                record.Id,
                relatedEntityType: "Training");
        }

        return RedirectToPage(new { id, message = "Assessment saved successfully.", success = true });
    }

    public async Task<IActionResult> OnPostRateTrainerAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();

        if (!await CanAccessTrainingAsync(record) || !CanRateTrainerValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        if (!TraineeRating.HasValue || TraineeRating < 1 || TraineeRating > 5)
            return RedirectToPage(new { id, message = "Rating must be between 1 and 5.", success = false });

        var meta = ParseNotes(record.Notes);
        meta.TraineeRating = TraineeRating;
        meta.TraineeFeedback = TraineeFeedback;
        meta.TraineeRatedAt = DateTime.UtcNow;
        record.Notes = JsonSerializer.Serialize(meta);
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        if (record.TrainerId.HasValue)
        {
            await _notificationService.SendAsync(
                record.TrainerId.Value,
                "New Trainer Rating Received",
                $"A trainee submitted trainer feedback for '{record.Title}'.",
                NotificationType.System,
                record.Id,
                relatedEntityType: "Training");
        }

        return RedirectToPage(new { id, message = "Trainer rating submitted successfully.", success = true });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();
        if (!await CanAccessTrainingAsync(record) || !CanApproveCompletionValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        var meta = ParseNotes(record.Notes);
        meta.CreatorDecision = "Approved";
        meta.CreatorDecisionComment = ApprovalComment;
        meta.CreatorDecisionAt = DateTime.UtcNow;
        record.Notes = JsonSerializer.Serialize(meta);
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Training approved.", success = true });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        var record = await GetRecordEntity(id);
        if (record == null) return NotFound();
        if (!await CanAccessTrainingAsync(record) || !CanApproveCompletionValue(record, _currentUserService.UserId))
            return RedirectToPage("/Account/AccessDenied");

        var meta = ParseNotes(record.Notes);
        meta.CreatorDecision = "Rejected";
        meta.CreatorDecisionComment = ApprovalComment;
        meta.CreatorDecisionAt = DateTime.UtcNow;
        record.Notes = JsonSerializer.Serialize(meta);
        record.LastModifiedById = _currentUserService.UserId;
        record.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = "Training rejected.", success = true });
    }

    private async Task<TrainingRecord?> GetRecordEntity(Guid id)
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

        var meta = ParseNotes(t.Notes);
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
            meta.PlainNotes,
            meta.CertificateFilePath,
            meta.CertificateFileName,
            meta.TrainerRemarks,
            meta.TraineeRating,
            meta.TraineeFeedback,
            meta.CreatorDecision,
            meta.CreatorDecisionComment,
            t.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
            t.UserId,
            t.TrainerId,
            t.CreatedById,
            t.Status == TrainingStatus.Scheduled);

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

    private async Task SetCapabilitiesAndDefaultsAsync(TrainingRecord record, bool overwriteInputs = true)
    {
        var currentUserId = _currentUserService.UserId;

        CanManageScheduledCrud = CanManageScheduled(record, currentUserId);
        CanStartTraining = CanStart(record, currentUserId);
        CanCompleteTraining = CanComplete(record, currentUserId);
        CanExpireTraining = CanExpire(record, currentUserId);
        CanUpdateCertificate = CanUpdateCertificateValue(record, currentUserId);
        CanSaveAssessment = CanSaveAssessmentValue(record, currentUserId);
        CanRateTrainer = CanRateTrainerValue(record, currentUserId);
        CanManageCertificateFile = CanManageCertificateFileValue(record, currentUserId);
        CanApproveCompletion = CanApproveCompletionValue(record, currentUserId);
        TrainingChatUrl = GetTrainingChatUrl(record, currentUserId);

        if (overwriteInputs)
        {
            EditTitle = record.Title;
            EditDescription = record.Description;
            EditTrainingType = record.TrainingType.ToString();
            EditUserId = record.UserId;
            EditScheduledDate = record.ScheduledDate;
            EditTrainerId = record.TrainerId;
            EditPassingScore = record.PassingScore;
        }

        if (CanManageScheduledCrud)
        {
            var tenantId = _currentUserService.TenantId
                ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

            Users = await _dbContext.Users.AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.IsActive)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .Select(u => new UserOption(u.Id, u.FirstName + " " + u.LastName))
                .ToListAsync();
        }
    }

    private bool IsCreator(TrainingRecord record, Guid? currentUserId)
        => currentUserId.HasValue && record.CreatedById == currentUserId.Value;

    private bool IsTrainee(TrainingRecord record, Guid? currentUserId)
        => currentUserId.HasValue && record.UserId == currentUserId.Value;

    private bool IsTrainer(TrainingRecord record, Guid? currentUserId)
        => currentUserId.HasValue && record.TrainerId.HasValue && record.TrainerId.Value == currentUserId.Value;

    private bool CanManageScheduled(TrainingRecord record, Guid? currentUserId)
        => record.Status == TrainingStatus.Scheduled && IsCreator(record, currentUserId);

    private bool CanStart(TrainingRecord record, Guid? currentUserId)
        => record.Status == TrainingStatus.Scheduled &&
           (IsCreator(record, currentUserId) || IsTrainer(record, currentUserId) || IsTrainee(record, currentUserId));

    private bool CanComplete(TrainingRecord record, Guid? currentUserId)
        => (record.Status == TrainingStatus.Scheduled || record.Status == TrainingStatus.InProgress) &&
           (IsCreator(record, currentUserId) || IsTrainer(record, currentUserId));

    private bool CanExpire(TrainingRecord record, Guid? currentUserId)
        => IsCreator(record, currentUserId) && record.Status != TrainingStatus.Completed && record.Status != TrainingStatus.Expired;

    private bool CanUpdateCertificateValue(TrainingRecord record, Guid? currentUserId)
        => IsCreator(record, currentUserId) || IsTrainer(record, currentUserId);

    private bool CanSaveAssessmentValue(TrainingRecord record, Guid? currentUserId)
        => (record.Status == TrainingStatus.InProgress || record.Status == TrainingStatus.Completed) &&
           (IsCreator(record, currentUserId) || IsTrainer(record, currentUserId));

    private bool CanRateTrainerValue(TrainingRecord record, Guid? currentUserId)
        => record.Status == TrainingStatus.Completed && IsTrainee(record, currentUserId);

    private bool CanManageCertificateFileValue(TrainingRecord record, Guid? currentUserId)
        => record.Status == TrainingStatus.Completed && (IsCreator(record, currentUserId) || IsTrainer(record, currentUserId));

    private bool CanApproveCompletionValue(TrainingRecord record, Guid? currentUserId)
        => record.Status == TrainingStatus.Completed && IsCreator(record, currentUserId);

    private static string? GetTrainingChatUrl(TrainingRecord record, Guid? currentUserId)
    {
        if (!currentUserId.HasValue) return null;
        if (record.UserId == currentUserId && record.TrainerId.HasValue) return $"/Chat?userId={record.TrainerId.Value}";
        if (record.TrainerId == currentUserId) return $"/Chat?userId={record.UserId}";
        return null;
    }

    private async Task<bool> CanAccessTrainingAsync(TrainingRecord record)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        if (IsCreator(record, currentUserId) || IsTrainer(record, currentUserId) || IsTrainee(record, currentUserId))
            return true;

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null) return false;

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrDeputy = roles.Any(r =>
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase));

        if (isTmdOrDeputy) return true;

        var isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));
        if (!isManager) return false;

        return await _dbContext.Users
            .AnyAsync(u => u.Id == record.UserId && u.ManagerId == currentUserId && u.IsActive);
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
        string? CertificateNumber, string? Notes, string? CertificateFilePath, string? CertificateFileName,
        string? TrainerRemarks, int? TraineeRating, string? TraineeFeedback,
        string? CreatorDecision, string? CreatorDecisionComment, string CreatedAt, Guid UserId,
        Guid? TrainerId, Guid CreatedById, bool IsScheduled);

    public record CompetencyRow(
        Guid Id, string CompetencyArea, string Level, int LevelValue,
        string Assessor, string AssessedDate, string NextAssessment);

    public record UserOption(Guid Id, string Name);

    private static TrainingWorkflowMeta ParseNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return new TrainingWorkflowMeta();
        try
        {
            var parsed = JsonSerializer.Deserialize<TrainingWorkflowMeta>(notes);
            if (parsed != null) return parsed;
        }
        catch (JsonException)
        {
            // Keep legacy plain notes when record contains non-JSON.
        }

        return new TrainingWorkflowMeta { PlainNotes = notes };
    }

    private sealed class TrainingWorkflowMeta
    {
        public string? PlainNotes { get; set; }
        public string? CertificateFilePath { get; set; }
        public string? CertificateFileName { get; set; }
        public string? TrainerRemarks { get; set; }
        public DateTime? TrainerAssessmentSubmittedAt { get; set; }
        public int? TraineeRating { get; set; }
        public string? TraineeFeedback { get; set; }
        public DateTime? TraineeRatedAt { get; set; }
        public string? CreatorDecision { get; set; }
        public string? CreatorDecisionComment { get; set; }
        public DateTime? CreatorDecisionAt { get; set; }
    }
}
