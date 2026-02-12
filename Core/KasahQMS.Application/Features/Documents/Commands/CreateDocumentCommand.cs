using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Documents;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Documents.Commands;

[Authorize(Permissions = Permissions.Documents.Create)]
public record CreateDocumentCommand(
    string Title,
    string? Description,
    string? Content,
    Guid? DocumentTypeId,
    Guid? CategoryId,
    string? FilePath = null,
    string? OriginalFileName = null,
    Guid? TargetDepartmentId = null,
    Guid? TargetUserId = null,
    Guid? SourceTemplateId = null) : IRequest<Result<Guid>>;

public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, Result<Guid>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDocumentCommandHandler> _logger;

    public CreateDocumentCommandHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<CreateDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (userId == null || tenantId == null)
            {
                return Result.Failure<Guid>(Error.Unauthorized);
            }

            // Generate document number
            var count = await _documentRepository.GetCountForYearAsync(tenantId.Value, DateTime.UtcNow.Year, cancellationToken);
            var documentNumber = $"DOC-{DateTime.UtcNow.Year}-{(count + 1):D5}";

            var document = Document.Create(
                tenantId.Value,
                request.Title,
                documentNumber,
                userId.Value,
                request.Description,
                request.DocumentTypeId,
                request.CategoryId);

            if (!string.IsNullOrWhiteSpace(request.FilePath))
                document.FilePath = request.FilePath;
            if (!string.IsNullOrWhiteSpace(request.OriginalFileName))
                document.OriginalFileName = request.OriginalFileName;
            if (request.TargetDepartmentId.HasValue)
                document.TargetDepartmentId = request.TargetDepartmentId;
            if (request.TargetUserId.HasValue)
                document.TargetUserId = request.TargetUserId;
            if (request.SourceTemplateId.HasValue)
                document.SourceTemplateId = request.SourceTemplateId;

            await _documentRepository.AddAsync(document, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "DOCUMENT_CREATED",
                "Documents",
                document.Id,
                $"Document '{request.Title}' created",
                cancellationToken);

            _logger.LogInformation("Document {DocumentId} created by user {UserId}", document.Id, userId);

            return Result.Success(document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating document");
            return Result.Failure<Guid>(Error.Custom("Document.CreateFailed", "Failed to create document."));
        }
    }
}
