using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Documents.Commands;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Documents;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IWebHostEnvironment _environment;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        IMediator mediator,
        ICurrentUserService currentUserService,
        IWebHostEnvironment environment,
        IAuditLogService auditLogService,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _environment = environment;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public DocumentDetailView? Document { get; set; }
    public List<DocumentAttachmentInfo> Attachments { get; set; } = new();
    public List<DocumentVersionInfo> Versions { get; set; } = new();
    public List<DocumentApprovalInfo> ApprovalHistory { get; set; } = new();
    public List<WorkflowRound> ApprovalTimelineRounds { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }
    
    /// <summary>
    /// Indicates if current user is viewing in read-only mode (auditors)
    /// </summary>
    public bool IsReadOnly { get; set; }
    public bool IsExecutive { get; set; }
    
    /// <summary>
    /// The user's role context for display purposes
    /// </summary>
    public string UserRoleContext { get; set; } = "Staff";

    public record DocumentAttachmentInfo(Guid Id, string FileName, string FilePath);
    public record DocumentVersionInfo(int VersionNumber, string? ChangeNotes, string CreatedAt, string CreatedBy);
    public record DocumentApprovalInfo(string ApproverName, bool IsApproved, string? Comments, string ApprovedAt);
    public record WorkflowTimelineEvent(DateTime Timestamp, string Action, string ActionClass, string Actor, string? Details);
    public record WorkflowRound(int RoundNumber, List<WorkflowTimelineEvent> Events);

    [BindProperty] public string? ApproveComments { get; set; }
    [BindProperty] public string? RejectReason { get; set; }
    [BindProperty] public string? ArchiveReason { get; set; }
    [BindProperty] public string? ReturnComments { get; set; }
    [BindProperty] public IFormFile? ReviewedFile { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
        {
            _logger.LogWarning("Document details accessed without valid user context");
            return Unauthorized();
        }

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        // Get current user with roles for authorization check
        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
        {
            _logger.LogWarning("Current user not found: {UserId}", currentUserId);
            return Unauthorized();
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        
        // Determine user's role context
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        bool isTmdOrDeputy = roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager" or "Deputy" or "DeputyDirector" or "Deputy Country Manager");
        bool isManager = roles.Any(r => r.Contains("Manager"));
        bool isAuditor = roles.Any(r => r == "Auditor");

        // Set role context for UI
        if (isAdmin) UserRoleContext = "Admin";
        else if (isTmdOrDeputy) UserRoleContext = "Executive";
        else if (isManager) UserRoleContext = "Manager";
        else if (isAuditor) UserRoleContext = "Auditor";
        else UserRoleContext = "Staff";

        IsReadOnly = isAuditor;
        IsExecutive = isTmdOrDeputy;

        // Load the document
        var doc = await _dbContext.Documents.AsNoTracking()
            .Include(d => d.ApprovedBy)
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (doc == null) return NotFound();

        // AUTHORIZATION CHECK: Can this user view this document?
        bool canView = false;

        if (isAdmin || isTmdOrDeputy)
        {
            // Admin/TMD/Deputy can view all documents
            canView = true;
        }
        else if (isAuditor)
        {
            // Auditors can view all documents (read-only for audit purposes)
            canView = true;
        }
        else if (doc.CreatedById == currentUserId || doc.CurrentApproverId == currentUserId)
        {
            // Creator or current approver can always view
            canView = true;
        }
        else if (isManager)
        {
            canView =
                doc.CreatedById == currentUserId.Value ||
                doc.CurrentApproverId == currentUserId.Value ||
                doc.TargetUserId == currentUserId.Value ||
                (currentUser.OrganizationUnitId.HasValue && doc.TargetDepartmentId == currentUser.OrganizationUnitId.Value);

            if (!canView && currentUser.OrganizationUnitId.HasValue)
            {
                var creatorOrgUnitId = await _dbContext.Users.AsNoTracking()
                    .Where(u => u.Id == doc.CreatedById)
                    .Select(u => u.OrganizationUnitId)
                    .FirstOrDefaultAsync();
                canView = creatorOrgUnitId == currentUser.OrganizationUnitId;
            }
        }
        else
        {
            canView =
                doc.CreatedById == currentUserId ||
                doc.CurrentApproverId == currentUserId ||
                doc.TargetUserId == currentUserId ||
                (currentUser.OrganizationUnitId.HasValue && doc.TargetDepartmentId == currentUser.OrganizationUnitId.Value);

            if (!canView && currentUser.OrganizationUnitId.HasValue)
            {
                var creatorOrgUnitId = await _dbContext.Users.AsNoTracking()
                    .Where(u => u.Id == doc.CreatedById)
                    .Select(u => u.OrganizationUnitId)
                    .FirstOrDefaultAsync();
                canView = creatorOrgUnitId == currentUser.OrganizationUnitId;
            }
        }

        if (!canView)
        {
            _logger.LogWarning("User {UserId} attempted to access document {DocumentId} without authorization", 
                currentUserId, id);
            return Forbid();
        }

        await LoadDocumentAsync(id, doc);
        
        _logger.LogInformation("User {UserId} ({RoleContext}) viewed document {DocumentId}", 
            currentUserId, UserRoleContext, id);
        
        return Document == null ? NotFound() : Page();
    }

    public async Task<IActionResult> OnPostSubmitAsync(Guid id)
    {
        var result = await _mediator.Send(new SubmitDocumentCommand(id));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Document moved to pending approval." : result.ErrorMessage, success = result.IsSuccess });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return RedirectToPage(new { id, message = "Unauthorized.", success = false });
        }

        var doc = await _dbContext.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null)
        {
            return RedirectToPage(new { id, message = "Document not found.", success = false });
        }

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (currentUser == null)
        {
            return RedirectToPage(new { id, message = "Unauthorized.", success = false });
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isExecutiveOverride = roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager" or "Deputy" or "DeputyDirector" or "Deputy Country Manager");
        if (!isExecutiveOverride && doc.CurrentApproverId != userId.Value)
        {
            return RedirectToPage(new { id, message = "Only assigned approver can approve this document.", success = false });
        }

        if (doc.Status != DocumentStatus.Submitted && doc.Status != DocumentStatus.InReview)
        {
            return RedirectToPage(new { id, message = "Only submitted/in-review documents can be approved.", success = false });
        }

        var result = await _mediator.Send(new ApproveDocumentCommand(id, ApproveComments));
        if (!result.IsSuccess)
        {
            return RedirectToPage(new { id, message = result.ErrorMessage, success = false });
        }

        if (ReviewedFile != null)
        {
            var attachResult = await SaveReviewedFileAsync(id, ReviewedFile);
            if (!attachResult.IsSuccess)
            {
                return RedirectToPage(new { id, message = "Document approved but reviewed file could not be attached.", success = false });
            }
        }

        return RedirectToPage(new { id, message = "Document approved.", success = true });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            return RedirectToPage(new { id, message = "Rejection reason is required.", success = false });
        }
        var result = await _mediator.Send(new RejectDocumentCommand(id, RejectReason));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Document rejected and returned to draft." : result.ErrorMessage, success = result.IsSuccess });
    }

    public async Task<IActionResult> OnPostReturnWithRevisionAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return RedirectToPage(new { id, message = "Unauthorized.", success = false });
        }

        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var doc = await _dbContext.Documents
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);
        if (doc == null)
        {
            return RedirectToPage(new { id, message = "Document not found.", success = false });
        }

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (currentUser == null)
        {
            return RedirectToPage(new { id, message = "Unauthorized.", success = false });
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isExecutiveOverride = roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager" or "Deputy" or "DeputyDirector" or "Deputy Country Manager");
        if (!isExecutiveOverride && doc.CurrentApproverId != userId.Value)
        {
            return RedirectToPage(new { id, message = "Only assigned approver can return this document.", success = false });
        }

        if (doc.Status != DocumentStatus.Submitted && doc.Status != DocumentStatus.InReview)
        {
            return RedirectToPage(new { id, message = "Only submitted/in-review documents can be returned.", success = false });
        }

        if (ReviewedFile == null || ReviewedFile.Length == 0)
        {
            return RedirectToPage(new { id, message = "Attach the reviewed/signed file before returning.", success = false });
        }

        var attachResult = await SaveReviewedFileAsync(id, ReviewedFile);
        if (!attachResult.IsSuccess)
        {
            return RedirectToPage(new { id, message = attachResult.ErrorMessage, success = false });
        }

        var reason = string.IsNullOrWhiteSpace(ReturnComments)
            ? "Returned with revised attachment by approver."
            : ReturnComments!;
        var rejectResult = await _mediator.Send(new RejectDocumentCommand(id, reason));
        if (!rejectResult.IsSuccess)
        {
            return RedirectToPage(new { id, message = rejectResult.ErrorMessage, success = false });
        }

        await _auditLogService.LogAsync(
            "DOCUMENT_RETURNED_WITH_REVISION",
            "Documents",
            id,
            $"Document returned with revised attachment by {userId}. Notes: {reason}",
            CancellationToken.None);

        return RedirectToPage(new { id, message = "Reviewed file returned to creator in the same approval thread.", success = true });
    }

    public async Task<IActionResult> OnPostArchiveAsync(Guid id)
    {
        var result = await _mediator.Send(new ArchiveDocumentCommand(id, ArchiveReason));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Document archived successfully." : result.ErrorMessage, success = result.IsSuccess });
    }

    private async Task LoadDocumentAsync(Guid id, KasahQMS.Domain.Entities.Documents.Document doc)
    {
        var creatorName = "Unknown";
        if (doc.CreatedById != Guid.Empty)
        {
            var creator = await _dbContext.Users.FindAsync(doc.CreatedById);
            if (creator != null) creatorName = creator.FullName;
        }

        Document = new DocumentDetailView(
            doc.Id,
            doc.DocumentNumber,
            doc.Title,
            doc.Description,
            doc.Content,
            doc.Status.ToString(),
            doc.CurrentVersion,
            creatorName,
            doc.CreatedAt.ToString("MMM dd, yyyy"),
            doc.ApprovedBy?.FullName,
            doc.ApprovedAt?.ToString("MMM dd, yyyy"),
            doc.FilePath,
            doc.OriginalFileName,
            doc.CreatedById,
            doc.CurrentApproverId,
            await ResolveSubmittedToAsync(doc)
        );

        // Load attachments
        Attachments = await _dbContext.DocumentAttachments.AsNoTracking()
            .Where(a => a.DocumentId == id)
            .OrderBy(a => a.OriginalFileName)
            .Select(a => new DocumentAttachmentInfo(a.Id, a.OriginalFileName, a.FilePath))
            .ToListAsync();

        // Load document versions
        Versions = await _dbContext.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionInfo(
                v.VersionNumber,
                v.ChangeNotes,
                v.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                v.CreatedByUser != null ? v.CreatedByUser.FirstName + " " + v.CreatedByUser.LastName : "Unknown"))
            .ToListAsync();

        // Load approval history
        ApprovalHistory = await _dbContext.DocumentApprovals.AsNoTracking()
            .Where(a => a.DocumentId == id)
            .OrderByDescending(a => a.ApprovedAt)
            .Select(a => new DocumentApprovalInfo(
                a.Approver != null ? a.Approver.FirstName + " " + a.Approver.LastName : "Unknown",
                a.IsApproved,
                a.Comments,
                a.ApprovedAt.ToString("MMM dd, yyyy HH:mm")))
            .ToListAsync();

        var auditTrail = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.EntityType == "Documents" && a.EntityId == id && (
                a.Action == "DOCUMENT_APPROVED_PARTIAL" ||
                a.Action == "DOCUMENT_APPROVED_FINAL" ||
                a.Action == "DOCUMENT_REJECTED" ||
                a.Action == "DOCUMENT_RETURNED_WITH_REVISION"))
            .OrderByDescending(a => a.Timestamp)
            .Take(30)
            .Select(a => new DocumentApprovalInfo(
                a.User != null ? a.User.FirstName + " " + a.User.LastName : "System",
                a.Action.Contains("APPROVED"),
                a.Description,
                a.Timestamp.ToString("MMM dd, yyyy HH:mm")))
            .ToListAsync();

        if (auditTrail.Count > 0)
        {
            ApprovalHistory = ApprovalHistory.Concat(auditTrail).ToList();
        }

        await LoadApprovalTimelineAsync(id);
    }

    /// <summary>
    /// Indicates if current user can submit this document for approval.
    /// Only creator can submit, and only if in Draft or Rejected state.
    /// Auditors cannot submit (read-only).
    /// </summary>
    public bool CanSubmit => Document != null 
        && !IsReadOnly
        && _currentUserService.UserId == Document.CreatedById
        && (Document.Status == "Draft" || Document.Status == "Rejected");

    /// <summary>
    /// Indicates if current user can approve or reject this document.
    /// Only current approver can approve/reject, and only if Submitted or InReview.
    /// Auditors cannot approve/reject (read-only).
    /// </summary>
    public bool CanApproveOrReject => Document != null 
        && !IsReadOnly
        && (IsExecutive || _currentUserService.UserId == Document.CurrentApproverId)
        && (Document.Status == "Submitted" || Document.Status == "InReview");

    public bool CanReturnWithRevision => CanApproveOrReject;

    /// <summary>
    /// Indicates if current user can edit this document.
    /// Only editable in Draft or Rejected state, and only by creator.
    /// Auditors cannot edit (read-only).
    /// </summary>
    public bool CanEdit => Document != null 
        && !IsReadOnly
        && _currentUserService.UserId == Document.CreatedById
        && Document.Status != "Archived";

    /// <summary>
    /// Indicates if current user can archive this document.
    /// Only approved documents can be archived, and only by admins/managers.
    /// </summary>
    public bool CanArchive => Document != null 
        && !IsReadOnly
        && Document.Status == "Approved"
        && (UserRoleContext is "Admin" or "Executive" or "Manager");

    public record DocumentDetailView(
        Guid Id,
        string Number,
        string Title,
        string? Description,
        string? Content,
        string Status,
        int Version,
        string Author,
        string CreatedDate,
        string? Approver,
        string? ApprovedDate,
        string? FilePath,
        string? OriginalFileName,
        Guid CreatedById,
        Guid? CurrentApproverId,
        string SubmittedTo
    );

    private async Task<string> ResolveSubmittedToAsync(KasahQMS.Domain.Entities.Documents.Document doc)
    {
        if (doc.CurrentApproverId.HasValue)
        {
            var approverName = await _dbContext.Users.AsNoTracking()
                .Where(u => u.Id == doc.CurrentApproverId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
            return !string.IsNullOrWhiteSpace(approverName) ? approverName! : "Pending approval queue";
        }

        if (doc.ApproverDepartmentId.HasValue)
        {
            var departmentName = await _dbContext.OrganizationUnits.AsNoTracking()
                .Where(ou => ou.Id == doc.ApproverDepartmentId.Value)
                .Select(ou => ou.Name)
                .FirstOrDefaultAsync();
            return !string.IsNullOrWhiteSpace(departmentName)
                ? $"{departmentName} department leaders"
                : "Department leaders";
        }

        if (doc.TargetUserId.HasValue)
        {
            var targetUserName = await _dbContext.Users.AsNoTracking()
                .Where(u => u.Id == doc.TargetUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(targetUserName)) return targetUserName!;
        }

        if (doc.TargetDepartmentId.HasValue)
        {
            var targetDepartmentName = await _dbContext.OrganizationUnits.AsNoTracking()
                .Where(ou => ou.Id == doc.TargetDepartmentId.Value)
                .Select(ou => ou.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(targetDepartmentName)) return $"{targetDepartmentName} department";
        }

        return "Not specified";
    }

    private async Task<(bool IsSuccess, string? ErrorMessage)> SaveReviewedFileAsync(Guid documentId, IFormFile file)
    {
        try
        {
            var root = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            var year = DateTime.UtcNow.Year.ToString();
            var uploadsDir = Path.Combine(root, "uploads", "documents", year);
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(ext)) ext = ".bin";
            var safeName = $"{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(uploadsDir, safeName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relPath = $"/uploads/documents/{year}/{safeName}";
            _dbContext.DocumentAttachments.Add(new KasahQMS.Domain.Entities.Documents.DocumentAttachment
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                FilePath = relPath,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                SourceDocumentId = documentId
            });
            await _dbContext.SaveChangesAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed saving reviewed file for document {DocumentId}", documentId);
            return (false, "Failed to save reviewed file.");
        }
    }

    private async Task LoadApprovalTimelineAsync(Guid documentId)
    {
        var entries = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.EntityType == "Documents"
                        && a.EntityId == documentId
                        && (a.Action == "DOCUMENT_SUBMITTED"
                            || a.Action == "DOCUMENT_APPROVED_PARTIAL"
                            || a.Action == "DOCUMENT_APPROVED_FINAL"
                            || a.Action == "DOCUMENT_REJECTED"
                            || a.Action == "DOCUMENT_RETURNED_WITH_REVISION"))
            .OrderBy(a => a.Timestamp)
            .Select(a => new
            {
                a.Timestamp,
                a.Action,
                a.Description,
                a.UserId
            })
            .ToListAsync();

        if (entries.Count == 0)
        {
            ApprovalTimelineRounds = new List<WorkflowRound>();
            return;
        }

        var userIds = entries.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).Distinct().ToList();
        var users = await _dbContext.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        var rounds = new List<WorkflowRound>();
        var currentRoundNumber = 0;
        List<WorkflowTimelineEvent>? currentRoundEvents = null;

        foreach (var entry in entries)
        {
            if (entry.Action == "DOCUMENT_SUBMITTED")
            {
                currentRoundNumber++;
                currentRoundEvents = new List<WorkflowTimelineEvent>();
                rounds.Add(new WorkflowRound(currentRoundNumber, currentRoundEvents));
            }
            else if (currentRoundEvents == null)
            {
                currentRoundNumber = 1;
                currentRoundEvents = new List<WorkflowTimelineEvent>();
                rounds.Add(new WorkflowRound(currentRoundNumber, currentRoundEvents));
            }

            var actor = entry.UserId.HasValue && users.TryGetValue(entry.UserId.Value, out var fullName)
                ? fullName
                : "System";

            currentRoundEvents!.Add(new WorkflowTimelineEvent(
                entry.Timestamp,
                GetTimelineActionLabel(entry.Action),
                GetTimelineActionClass(entry.Action),
                actor,
                entry.Description));
        }

        ApprovalTimelineRounds = rounds
            .OrderByDescending(r => r.RoundNumber)
            .Select(r => new WorkflowRound(
                r.RoundNumber,
                r.Events.OrderByDescending(e => e.Timestamp).ToList()))
            .ToList();
    }

    private static string GetTimelineActionLabel(string action)
        => action switch
        {
            "DOCUMENT_SUBMITTED" => "Pending Approval",
            "DOCUMENT_APPROVED_PARTIAL" => "Partially Approved",
            "DOCUMENT_APPROVED_FINAL" => "Final Approval",
            "DOCUMENT_REJECTED" => "Rejected / Returned",
            "DOCUMENT_RETURNED_WITH_REVISION" => "Returned with Revised File",
            _ => action
        };

    private static string GetTimelineActionClass(string action)
        => action switch
        {
            "DOCUMENT_SUBMITTED" => "bg-amber-100 text-amber-700",
            "DOCUMENT_APPROVED_PARTIAL" => "bg-blue-100 text-blue-700",
            "DOCUMENT_APPROVED_FINAL" => "bg-emerald-100 text-emerald-700",
            "DOCUMENT_REJECTED" => "bg-rose-100 text-rose-700",
            "DOCUMENT_RETURNED_WITH_REVISION" => "bg-indigo-100 text-indigo-700",
            _ => "bg-slate-100 text-slate-700"
        };
}
