using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Documents.Commands;

public record UpdateDocumentCommand(
    Guid DocumentId,
    string? Title,
    string? Description,
    string? Content) : IRequest<Result>;

public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, Result>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateDocumentCommandHandler> _logger;

    public UpdateDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<UpdateDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
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

            // Enforce read-only: Approved documents cannot be edited (must create new version)
            if (document.Status == DocumentStatus.Approved)
            {
                return Result.Failure(Error.Custom("Document.ReadOnly", 
                    "Approved documents are read-only. Create a new version to make changes."));
            }

            // Only creator or current approver can edit
            if (document.CreatedById != userId.Value && document.CurrentApproverId != userId.Value)
            {
                return Result.Failure(Error.Forbidden);
            }

            // Update document
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                document.UpdateTitle(request.Title);
            }

            if (request.Description != null)
            {
                document.UpdateDescription(request.Description);
            }

            if (request.Content != null)
            {
                document.UpdateContent(request.Content);
            }

            await _documentRepository.UpdateAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "DOCUMENT_UPDATED",
                "Documents",
                document.Id,
                $"Document '{document.Title}' updated",
                cancellationToken);

            _logger.LogInformation("Document {DocumentId} updated by user {UserId}", document.Id, userId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating document {DocumentId}", request.DocumentId);
            return Result.Failure(Error.Custom("Document.UpdateFailed", "Failed to update document."));
        }
    }
}

