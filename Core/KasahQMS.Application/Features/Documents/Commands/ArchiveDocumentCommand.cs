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

/// <summary>
/// Command to archive an approved document.
/// Only approved documents can be archived.
/// Archived documents become read-only and are moved out of active workflows.
/// </summary>
[Authorize(Permissions = Permissions.Documents.Archive)]
public record ArchiveDocumentCommand(
    Guid DocumentId,
    string? ArchiveReason = null) : IRequest<Result>;

public class ArchiveDocumentCommandHandler : IRequestHandler<ArchiveDocumentCommand, Result>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveDocumentCommandHandler> _logger;

    public ArchiveDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<ArchiveDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ArchiveDocumentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId == null)
            {
                return Result.Failure(Error.Unauthorized);
            }

            var document = await _documentRepository.GetByIdWithDetailsAsync(request.DocumentId, cancellationToken);
            if (document == null)
            {
                return Result.Failure(Error.NotFound);
            }

            if (document.TenantId != _currentUserService.TenantId)
            {
                return Result.Failure(Error.Forbidden);
            }

            // Only approved documents can be archived
            if (document.Status != DocumentStatus.Approved)
            {
                return Result.Failure(Error.Custom("Document.NotApproved", 
                    "Only approved documents can be archived. Current status: " + document.Status));
            }

            // Archive the document
            document.Archive(userId.Value, request.ArchiveReason);
            
            await _documentRepository.UpdateAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Log the action
            await _auditLogService.LogAsync(
                "DOCUMENT_ARCHIVED",
                "Documents",
                document.Id,
                $"Document '{document.Title}' archived. Reason: {request.ArchiveReason ?? "No reason provided"}",
                cancellationToken);

            // Notify the document creator
            await _notificationService.SendAsync(
                document.CreatedById,
                "Document Archived",
                $"Your document '{document.Title}' has been archived.",
                NotificationType.System,
                document.Id,
                cancellationToken);

            _logger.LogInformation("Document {DocumentId} archived by user {UserId}. Reason: {Reason}", 
                document.Id, userId, request.ArchiveReason);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving document {DocumentId}", request.DocumentId);
            return Result.Failure(Error.Custom("Document.ArchiveFailed", "Failed to archive document."));
        }
    }
}
