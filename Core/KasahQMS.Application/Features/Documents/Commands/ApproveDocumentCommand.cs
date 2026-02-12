using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Documents.Commands;

[Authorize(Permissions = Permissions.Documents.Approve)]
public record ApproveDocumentCommand(Guid DocumentId, string? Comments) : IRequest<Result>;

public class ApproveDocumentCommandHandler : IRequestHandler<ApproveDocumentCommand, Result>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IWorkflowService _workflowService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveDocumentCommandHandler> _logger;

    public ApproveDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IWorkflowService workflowService,
        IUnitOfWork unitOfWork,
        ILogger<ApproveDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _workflowService = workflowService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ApproveDocumentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId == null)
            {
                return Result.Failure(Error.Unauthorized);
            }

            var document = await _documentRepository.GetByIdAsync(request.DocumentId, cancellationToken);
            if (document == null)
            {
                return Result.Failure(Error.NotFound);
            }

            if (document.TenantId != _currentUserService.TenantId)
            {
                return Result.Failure(Error.Forbidden);
            }

            if (document.Status != DocumentStatus.Submitted && document.Status != DocumentStatus.InReview)
            {
                return Result.Failure(Error.Conflict);
            }

            // Check if current user is the assigned approver
            if (document.CurrentApproverId != userId.Value)
            {
                return Result.Failure(Error.Forbidden);
            }

            // Check if more approvals are required
            bool requiresMoreApprovals = false;
            if (document.DocumentTypeId.HasValue)
            {
                requiresMoreApprovals = await _workflowService.RequiresAdditionalApprovalsAsync(
                    document.Id, 
                    userId.Value, 
                    cancellationToken);
            }

            if (requiresMoreApprovals)
            {
                // Record partial approval (not final)
                document.RecordPartialApproval(userId.Value, request.Comments);
                
                // Route to next approver
                var nextApproverId = await _workflowService.GetNextApproverAsync(
                    document.Id, 
                    document.DocumentTypeId!.Value, 
                    cancellationToken);

                if (nextApproverId.HasValue)
                {
                    document.Status = DocumentStatus.InReview;
                    document.CurrentApproverId = nextApproverId;
                    document.ApprovedById = null; // Clear final approval until last approver
                    document.ApprovedAt = null;

                    await _auditLogService.LogAsync(
                        "DOCUMENT_APPROVED_PARTIAL",
                        "Documents",
                        document.Id,
                        $"Document '{document.Title}' approved by {userId}, routed to next approver",
                        cancellationToken);

                    // Notify next approver
                    await _notificationService.SendAsync(
                        nextApproverId.Value,
                        "Document Pending Approval",
                        $"Document '{document.Title}' requires your approval.",
                        NotificationType.DocumentApproval,
                        document.Id,
                        cancellationToken);
                }
            }
            else
            {
                // Final approval - document is now approved and read-only
                document.Approve(userId.Value, request.Comments);
                document.EffectiveDate = DateTime.UtcNow;

                await _auditLogService.LogAsync(
                    "DOCUMENT_APPROVED_FINAL",
                    "Documents",
                    document.Id,
                    $"Document '{document.Title}' fully approved and sealed",
                    cancellationToken);

                // Notify document owner
                await _notificationService.SendAsync(
                    document.CreatedById,
                    "Document Approved",
                    $"Your document '{document.Title}' has been fully approved and is now effective.",
                    NotificationType.DocumentApproval,
                    document.Id,
                    cancellationToken);
            }

            await _documentRepository.UpdateAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document {DocumentId} approved by user {UserId}, requiresMoreApprovals: {RequiresMore}", 
                document.Id, userId, requiresMoreApprovals);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving document {DocumentId}", request.DocumentId);
            return Result.Failure(Error.Custom("Document.ApproveFailed", "Failed to approve document."));
        }
    }
}
