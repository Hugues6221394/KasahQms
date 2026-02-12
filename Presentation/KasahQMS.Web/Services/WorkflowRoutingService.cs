using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Services;

/// <summary>
/// Service for managing document approval workflows and automatic routing.
/// Automatically creates approval tasks and routes documents based on hierarchy.
/// Implements the Tender Requisition workflow and other document types.
/// </summary>
public interface IWorkflowRoutingService
{
    /// <summary>
    /// Route document for approval based on workflow rules.
    /// For Tender Requisitions: Tender Lead → Finance → Deputy/TMD
    /// For other documents: Based on DocumentTypeApprover configuration or manager hierarchy
    /// </summary>
    Task<(bool Success, string? ErrorMessage, Guid? NextApproverId)> RouteDocumentForApprovalAsync(Guid documentId, Guid submittedBy);

    /// <summary>
    /// Get next approver in workflow chain after current approval step.
    /// </summary>
    Task<Guid?> GetNextApproverAsync(Guid documentId);

    /// <summary>
    /// Get pending approvals for user.
    /// </summary>
    Task<List<ApprovalTaskInfo>> GetPendingApprovalsAsync(Guid userId);

    /// <summary>
    /// Get approval history for document.
    /// </summary>
    Task<List<ApprovalHistory>> GetApprovalHistoryAsync(Guid documentId);

    /// <summary>
    /// Process approval and route to next approver if needed.
    /// Returns true if document is fully approved, false if more approvals needed.
    /// </summary>
    Task<(bool FullyApproved, Guid? NextApproverId, string? Message)> ProcessApprovalAsync(
        Guid documentId, 
        Guid approverId, 
        bool isApproved, 
        string? comments);

    /// <summary>
    /// Create an approval task for the specified approver.
    /// </summary>
    Task<Guid?> CreateApprovalTaskAsync(
        Guid documentId, 
        Guid approverId, 
        string description,
        TaskPriority priority = TaskPriority.High,
        int dueDays = 3);

    /// <summary>
    /// Determines if document type requires Finance approval (e.g., Tender Requisitions).
    /// </summary>
    Task<bool> RequiresFinanceApprovalAsync(Guid documentId);
}

public class WorkflowRoutingService : IWorkflowRoutingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHierarchyService _hierarchyService;
    private readonly ILogger<WorkflowRoutingService> _logger;

    // Well-known role names for workflow routing
    private static readonly string[] FinanceRoles = { "Finance Manager", "Finance, Accounting & Logistics Manager", "Finance" };
    private static readonly string[] TmdRoles = { "TMD", "TopManagingDirector", "Country Manager", "Top Managing Director" };
    private static readonly string[] DeputyRoles = { "Deputy", "DeputyDirector", "Deputy Country Manager" };

    public WorkflowRoutingService(
        ApplicationDbContext dbContext,
        IHierarchyService hierarchyService,
        ILogger<WorkflowRoutingService> logger)
    {
        _dbContext = dbContext;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    public async Task<(bool Success, string? ErrorMessage, Guid? NextApproverId)> RouteDocumentForApprovalAsync(Guid documentId, Guid submittedBy)
    {
        var document = await _dbContext.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return (false, "Document not found.", null);

        var submitter = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == submittedBy);

        if (submitter == null)
            return (false, "Submitter not found.", null);

        try
        {
            Guid? nextApproverId = null;

            // Determine workflow based on document type/category
            bool isTenderRequisition = IsTenderDocument(document);

            if (isTenderRequisition)
            {
                // Tender Requisition workflow:
                // Step 1: Route to Finance for budget & compliance review
                nextApproverId = await FindFinanceManagerAsync(document.TenantId);
                
                if (nextApproverId == null)
                {
                    _logger.LogWarning("No Finance Manager found for tender requisition routing. Falling back to TMD/Deputy.");
                    nextApproverId = await FindTmdOrDeputyAsync(document.TenantId);
                }

                _logger.LogInformation(
                    "Tender requisition {DocumentId} routed to Finance ({ApproverId}) for review",
                    documentId, nextApproverId);
            }
            else
            {
                // Standard workflow: Check DocumentTypeApprover first, then fall back to manager hierarchy
                if (document.DocumentTypeId.HasValue)
                {
                    nextApproverId = await GetFirstApproverFromWorkflowAsync(document.DocumentTypeId.Value);
                }

                // If no configured approvers, use manager hierarchy
                if (nextApproverId == null && submitter.ManagerId.HasValue)
                {
                    nextApproverId = submitter.ManagerId.Value;
                    _logger.LogInformation(
                        "Document {DocumentId} routed to submitter's manager ({ManagerId})",
                        documentId, nextApproverId);
                }

                // Final fallback: TMD/Deputy
                if (nextApproverId == null)
                {
                    nextApproverId = await FindTmdOrDeputyAsync(document.TenantId);
                }
            }

            if (nextApproverId.HasValue)
            {
                // Create approval task for the approver
                await CreateApprovalTaskAsync(
                    documentId, 
                    nextApproverId.Value, 
                    isTenderRequisition 
                        ? $"Review tender requisition: {document.Title}" 
                        : $"Approve document: {document.Title}",
                    isTenderRequisition ? TaskPriority.High : TaskPriority.Medium,
                    isTenderRequisition ? 3 : 5);

                // Send notification
                await CreateNotificationAsync(
                    nextApproverId.Value,
                    isTenderRequisition ? "Tender Requisition Review Required" : "Document Awaiting Your Approval",
                    $"Document '{document.Title}' requires your review and approval.",
                    NotificationType.DocumentApproval,
                    documentId);
            }

            _logger.LogInformation(
                "Document {DocumentId} routed for approval by {SubmitterId}. Next approver: {NextApproverId}",
                documentId, submittedBy, nextApproverId);

            return (true, null, nextApproverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing document {DocumentId} for approval", documentId);
            return (false, "An error occurred while routing document.", null);
        }
    }

    public async Task<Guid?> GetNextApproverAsync(Guid documentId)
    {
        var document = await _dbContext.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return null;

        // Check if this is a tender requisition that needs to go to TMD/Deputy after Finance
        bool isTenderRequisition = IsTenderDocument(document);
        if (isTenderRequisition && document.CurrentApproverId.HasValue)
        {
            // Check if current approver is Finance
            var currentApprover = await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == document.CurrentApproverId.Value);

            if (currentApprover?.Roles?.Any(r => FinanceRoles.Contains(r.Name)) == true)
            {
                // Next approver is TMD or Deputy
                return await FindTmdOrDeputyAsync(document.TenantId);
            }
        }

        // Standard workflow: Check DocumentTypeApprover
        if (document.DocumentTypeId.HasValue && document.CurrentApproverId.HasValue)
        {
            return await GetNextApproverFromWorkflowAsync(
                document.DocumentTypeId.Value, 
                document.CurrentApproverId.Value);
        }

        // No more approvers
        return null;
    }

    public async Task<List<ApprovalTaskInfo>> GetPendingApprovalsAsync(Guid userId)
    {
        var pendingDocs = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.CurrentApproverId == userId && 
                        (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview))
            .OrderByDescending(d => d.SubmittedAt)
            .Select(d => new ApprovalTaskInfo
            {
                ApprovalTaskId = Guid.Empty, // Would be populated from QmsTasks
                DocumentId = d.Id,
                DocumentTitle = d.Title,
                CreatedAt = d.SubmittedAt ?? d.CreatedAt,
                DueDate = d.SubmittedAt.HasValue ? d.SubmittedAt.Value.AddDays(5) : null,
                Instructions = $"Review and approve document {d.DocumentNumber}",
                IsOverdue = d.SubmittedAt.HasValue && d.SubmittedAt.Value.AddDays(5) < DateTime.UtcNow
            })
            .ToListAsync();

        // Link to actual approval tasks if they exist
        foreach (var pending in pendingDocs)
        {
            var task = await _dbContext.QmsTasks
                .Where(t => t.LinkedDocumentId == pending.DocumentId && 
                           t.AssignedToId == userId && 
                           t.Status != QmsTaskStatus.Completed && 
                           t.Status != QmsTaskStatus.Cancelled)
                .FirstOrDefaultAsync();

            if (task != null)
            {
                pending.ApprovalTaskId = task.Id;
                pending.DueDate = task.DueDate;
                pending.IsOverdue = task.DueDate.HasValue && task.DueDate.Value < DateTime.UtcNow;
            }
        }

        return pendingDocs;
    }

    public async Task<List<ApprovalHistory>> GetApprovalHistoryAsync(Guid documentId)
    {
        var approvals = await _dbContext.DocumentApprovals
            .AsNoTracking()
            .Include(a => a.Approver)
            .Where(a => a.DocumentId == documentId)
            .OrderByDescending(a => a.ApprovedAt)
            .Select(a => new ApprovalHistory
            {
                ApproverName = a.Approver != null ? a.Approver.FirstName + " " + a.Approver.LastName : "Unknown",
                Status = a.IsApproved ? "Approved" : "Rejected",
                Comments = a.Comments,
                CreatedAt = a.ApprovedAt,
                CompletedAt = a.ApprovedAt
            })
            .ToListAsync();

        return approvals;
    }

    public async Task<(bool FullyApproved, Guid? NextApproverId, string? Message)> ProcessApprovalAsync(
        Guid documentId, 
        Guid approverId, 
        bool isApproved, 
        string? comments)
    {
        var document = await _dbContext.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Category)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return (false, null, "Document not found.");

        if (document.CurrentApproverId != approverId)
            return (false, null, "You are not the current approver for this document.");

        // Record the approval
        var approval = new DocumentApproval
        {
            DocumentId = documentId,
            ApproverId = approverId,
            IsApproved = isApproved,
            Comments = comments,
            ApprovedAt = DateTime.UtcNow
        };
        _dbContext.DocumentApprovals.Add(approval);

        if (!isApproved)
        {
            // Document rejected - return to draft
            document.Reject(approverId, comments ?? "Rejected by approver");
            await _dbContext.SaveChangesAsync();

            // Notify creator
            await CreateNotificationAsync(
                document.CreatedById,
                "Document Rejected",
                $"Your document '{document.Title}' has been rejected. Reason: {comments}",
                NotificationType.DocumentRejection,
                documentId);

            // Complete any pending approval tasks
            await CompleteApprovalTasksAsync(documentId, approverId, false);

            return (false, null, "Document rejected and returned to draft.");
        }

        // Check if more approvals are needed
        var nextApproverId = await GetNextApproverAsync(documentId);

        if (nextApproverId.HasValue)
        {
            // More approvals needed - route to next approver
            document.RecordPartialApproval(approverId, comments);
            document.CurrentApproverId = nextApproverId.Value;
            await _dbContext.SaveChangesAsync();

            // Complete current approval task
            await CompleteApprovalTasksAsync(documentId, approverId, true);

            // Create task for next approver
            bool isTenderRequisition = IsTenderDocument(document);
            await CreateApprovalTaskAsync(
                documentId,
                nextApproverId.Value,
                isTenderRequisition 
                    ? $"Final approval: {document.Title}" 
                    : $"Approve document: {document.Title}",
                TaskPriority.High,
                3);

            // Notify next approver
            await CreateNotificationAsync(
                nextApproverId.Value,
                "Document Awaiting Your Approval",
                $"Document '{document.Title}' requires your approval. Previous approver has approved.",
                NotificationType.DocumentApproval,
                documentId);

            _logger.LogInformation(
                "Document {DocumentId} partially approved by {ApproverId}. Routed to {NextApproverId}",
                documentId, approverId, nextApproverId);

            return (false, nextApproverId.Value, "Document approved. Routed to next approver.");
        }

        // Final approval - no more approvers
        document.Approve(approverId, comments);
        await _dbContext.SaveChangesAsync();

        // Complete approval task
        await CompleteApprovalTasksAsync(documentId, approverId, true);

        // Notify creator of final approval
        await CreateNotificationAsync(
            document.CreatedById,
            "Document Approved",
            $"Your document '{document.Title}' has been fully approved and is now effective.",
            NotificationType.DocumentApproval,
            documentId);

        // For tender requisitions, create implementation tasks
        if (IsTenderDocument(document))
        {
            await CreateTenderImplementationTasksAsync(document, approverId);
        }

        _logger.LogInformation(
            "Document {DocumentId} fully approved by {ApproverId}",
            documentId, approverId);

        return (true, null, "Document fully approved.");
    }

    public async Task<Guid?> CreateApprovalTaskAsync(
        Guid documentId, 
        Guid approverId, 
        string description,
        TaskPriority priority = TaskPriority.High,
        int dueDays = 3)
    {
        try
        {
            var document = await _dbContext.Documents.FindAsync(documentId);
            if (document == null) return null;

            var tenantId = document.TenantId;
            var count = await _dbContext.QmsTasks.CountAsync(t => 
                t.TenantId == tenantId && 
                t.CreatedAt.Year == DateTime.UtcNow.Year);

            var taskNumber = $"TASK-{DateTime.UtcNow.Year}-{(count + 1):D5}";

            var task = QmsTask.Create(
                tenantId,
                description,
                taskNumber,
                Guid.Empty, // System created
                $"Review and approve document: {document.DocumentNumber}",
                priority,
                DateTime.UtcNow.AddDays(dueDays));

            task.Assign(approverId);
            task.LinkToDocument(documentId);
            task.AddTag("approval");
            task.AddTag("workflow");

            await _dbContext.QmsTasks.AddAsync(task);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Created approval task {TaskId} for document {DocumentId}, assigned to {ApproverId}",
                task.Id, documentId, approverId);

            return task.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create approval task for document {DocumentId}", documentId);
            return null;
        }
    }

    public async Task<bool> RequiresFinanceApprovalAsync(Guid documentId)
    {
        var document = await _dbContext.Documents
            .Include(d => d.Category)
            .Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        return document != null && IsTenderDocument(document);
    }

    #region Private Helper Methods

    private bool IsTenderDocument(Document document)
    {
        var categoryName = document.Category?.Name ?? "";
        var typeName = document.DocumentType?.Name ?? "";
        var title = document.Title ?? "";

        return categoryName.Contains("Tender", StringComparison.OrdinalIgnoreCase) ||
               typeName.Contains("Tender", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("Tender Requisition", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid?> FindFinanceManagerAsync(Guid tenantId)
    {
        var financeUser = await _dbContext.Users
            .Include(u => u.Roles)
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Where(u => u.Roles!.Any(r => FinanceRoles.Contains(r.Name)))
            .FirstOrDefaultAsync();

        return financeUser?.Id;
    }

    private async Task<Guid?> FindTmdOrDeputyAsync(Guid tenantId)
    {
        // Try to find TMD first
        var tmd = await _dbContext.Users
            .Include(u => u.Roles)
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Where(u => u.Roles!.Any(r => TmdRoles.Contains(r.Name)))
            .FirstOrDefaultAsync();

        if (tmd != null) return tmd.Id;

        // Fall back to Deputy
        var deputy = await _dbContext.Users
            .Include(u => u.Roles)
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Where(u => u.Roles!.Any(r => DeputyRoles.Contains(r.Name)))
            .FirstOrDefaultAsync();

        return deputy?.Id;
    }

    private async Task<Guid?> GetFirstApproverFromWorkflowAsync(Guid documentTypeId)
    {
        var firstApprover = await _dbContext.Set<DocumentTypeApprover>()
            .Where(dta => dta.DocumentTypeId == documentTypeId && dta.IsRequired)
            .OrderBy(dta => dta.ApprovalOrder)
            .Select(dta => dta.ApproverId)
            .FirstOrDefaultAsync();

        return firstApprover == Guid.Empty ? null : firstApprover;
    }

    private async Task<Guid?> GetNextApproverFromWorkflowAsync(Guid documentTypeId, Guid currentApproverId)
    {
        var approvers = await _dbContext.Set<DocumentTypeApprover>()
            .Where(dta => dta.DocumentTypeId == documentTypeId && dta.IsRequired)
            .OrderBy(dta => dta.ApprovalOrder)
            .Select(dta => dta.ApproverId)
            .ToListAsync();

        var currentIndex = approvers.IndexOf(currentApproverId);
        if (currentIndex < 0 || currentIndex >= approvers.Count - 1)
        {
            return null;
        }

        return approvers[currentIndex + 1];
    }

    private async Task CreateNotificationAsync(
        Guid userId, 
        string title, 
        string message, 
        NotificationType type, 
        Guid? relatedEntityId)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = "Document",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.Notifications.AddAsync(notification);
        await _dbContext.SaveChangesAsync();
    }

    private async Task CompleteApprovalTasksAsync(Guid documentId, Guid approverId, bool approved)
    {
        var tasks = await _dbContext.QmsTasks
            .Where(t => t.LinkedDocumentId == documentId && 
                       t.AssignedToId == approverId &&
                       t.Status != QmsTaskStatus.Completed &&
                       t.Status != QmsTaskStatus.Cancelled)
            .ToListAsync();

        foreach (var task in tasks)
        {
            task.Complete(approverId, approved ? "Approved" : "Rejected");
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task CreateTenderImplementationTasksAsync(Document document, Guid approverId)
    {
        try
        {
            var tenantId = document.TenantId;
            var count = await _dbContext.QmsTasks.CountAsync(t => 
                t.TenantId == tenantId && 
                t.CreatedAt.Year == DateTime.UtcNow.Year);

            // Create implementation task
            var taskNumber = $"TASK-{DateTime.UtcNow.Year}-{(count + 1):D5}";
            var implementationTask = QmsTask.Create(
                tenantId,
                $"Implement approved tender: {document.Title}",
                taskNumber,
                approverId,
                $"Tender requisition {document.DocumentNumber} has been approved. Begin implementation.",
                TaskPriority.High,
                DateTime.UtcNow.AddDays(14));

            implementationTask.LinkToDocument(document.Id);
            implementationTask.AddTag("implementation");
            implementationTask.AddTag("tender");

            // Assign to document creator (Tender Lead)
            implementationTask.Assign(document.CreatedById);

            await _dbContext.QmsTasks.AddAsync(implementationTask);
            await _dbContext.SaveChangesAsync();

            // Notify Tender Lead
            await CreateNotificationAsync(
                document.CreatedById,
                "Tender Approved - Implementation Required",
                $"Your tender requisition '{document.Title}' has been fully approved. An implementation task has been created.",
                NotificationType.TaskAssignment,
                implementationTask.Id);

            _logger.LogInformation(
                "Created implementation task {TaskId} for approved tender {DocumentId}",
                implementationTask.Id, document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create implementation tasks for tender {DocumentId}", document.Id);
        }
    }

    #endregion
}

/// <summary>
/// DTO for pending approvals.
/// </summary>
public class ApprovalTaskInfo
{
    public Guid ApprovalTaskId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Instructions { get; set; }
    public bool IsOverdue { get; set; }
}

/// <summary>
/// DTO for approval history.
/// </summary>
public class ApprovalHistory
{
    public string ApproverName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
