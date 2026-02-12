using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Features.Documents.Commands;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
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
    private readonly IHierarchyService _hierarchyService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        IMediator mediator,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    public DocumentDetailView? Document { get; set; }
    public List<DocumentAttachmentInfo> Attachments { get; set; } = new();
    public List<DocumentVersionInfo> Versions { get; set; } = new();
    public List<DocumentApprovalInfo> ApprovalHistory { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }
    
    /// <summary>
    /// Indicates if current user is viewing in read-only mode (auditors)
    /// </summary>
    public bool IsReadOnly { get; set; }
    
    /// <summary>
    /// The user's role context for display purposes
    /// </summary>
    public string UserRoleContext { get; set; } = "Staff";

    public record DocumentAttachmentInfo(Guid Id, string FileName, string FilePath);
    public record DocumentVersionInfo(int VersionNumber, string? ChangeNotes, string CreatedAt, string CreatedBy);
    public record DocumentApprovalInfo(string ApproverName, bool IsApproved, string? Comments, string ApprovedAt);

    [BindProperty] public string? ApproveComments { get; set; }
    [BindProperty] public string? RejectReason { get; set; }
    [BindProperty] public string? ArchiveReason { get; set; }

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
            // Managers can view documents created by their subordinates
            var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(currentUserId.Value);
            canView = subordinateIds.Contains(doc.CreatedById);
            
            // Also check if document is targeted to their department or no specific department
            if (!canView && (doc.TargetDepartmentId == null || doc.TargetDepartmentId == currentUser.OrganizationUnitId))
            {
                canView = true;
            }
        }
        else
        {
            // Staff can view documents they created or that are targeted to their department
            canView = doc.CreatedById == currentUserId || 
                      doc.TargetDepartmentId == null || 
                      doc.TargetDepartmentId == currentUser.OrganizationUnitId;
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
        return RedirectToPage(new { id, message = result.IsSuccess ? "Document submitted for approval." : result.ErrorMessage, success = result.IsSuccess });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var result = await _mediator.Send(new ApproveDocumentCommand(id, ApproveComments));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Document approved." : result.ErrorMessage, success = result.IsSuccess });
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
            doc.CurrentApproverId
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
        && _currentUserService.UserId == Document.CurrentApproverId
        && (Document.Status == "Submitted" || Document.Status == "InReview");

    /// <summary>
    /// Indicates if current user can edit this document.
    /// Only editable in Draft or Rejected state, and only by creator.
    /// Auditors cannot edit (read-only).
    /// </summary>
    public bool CanEdit => Document != null 
        && !IsReadOnly
        && _currentUserService.UserId == Document.CreatedById
        && (Document.Status == "Draft" || Document.Status == "Rejected");

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
        Guid? CurrentApproverId
    );
}
