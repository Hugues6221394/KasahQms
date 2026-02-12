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

[Authorize(Permissions = Permissions.Documents.Reject)]
public record RejectDocumentCommand(Guid DocumentId, string Reason) : IRequest<Result>;

public class RejectDocumentCommandHandler : IRequestHandler<RejectDocumentCommand, Result>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectDocumentCommandHandler> _logger;

    public RejectDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<RejectDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(RejectDocumentCommand request, CancellationToken cancellationToken)
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
                return Result.Failure(Error.Custom("Document.InvalidStatus",
                    $"Cannot reject document in {document.Status} status."));
            }

            // Check if current user is the assigned approver
            if (document.CurrentApproverId != userId.Value)
            {
                return Result.Failure(Error.Forbidden);
            }

            document.Reject(userId.Value, request.Reason);
            await _documentRepository.UpdateAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "DOCUMENT_REJECTED",
                "Documents",
                document.Id,
                $"Document '{document.Title}' rejected: {request.Reason}",
                cancellationToken);

            // Notify document owner
            await _notificationService.SendAsync(
                document.CreatedById,
                "Document Rejected",
                $"Your document '{document.Title}' has been rejected. Reason: {request.Reason}",
                NotificationType.DocumentRejection,
                document.Id,
                cancellationToken);

            _logger.LogInformation("Document {DocumentId} rejected by user {UserId}", document.Id, userId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting document {DocumentId}", request.DocumentId);
            return Result.Failure(Error.Custom("Document.RejectFailed", "Failed to reject document."));
        }
    }
}
