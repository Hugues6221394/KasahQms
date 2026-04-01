using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Security;
using KasahQMS.Application.Features.Documents.Dtos;
using KasahQMS.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Documents.Queries;

[Authorize(Permissions = $"{Permissions.Documents.View}, {Permissions.Documents.ViewAll}")]
public record GetDocumentQuery(Guid DocumentId) : IRequest<Result<DocumentDto>>;

public class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, Result<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetDocumentQueryHandler> _logger;

    public GetDocumentQueryHandler(
        IDocumentRepository documentRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        ILogger<GetDocumentQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<DocumentDto>> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var document = await _documentRepository.GetByIdWithDetailsAsync(request.DocumentId, cancellationToken);

            if (document == null)
            {
                return Result.Failure<DocumentDto>(Error.NotFound);
            }

            if (document.TenantId != _currentUserService.TenantId)
            {
                return Result.Failure<DocumentDto>(Error.Forbidden);
            }

            var userId = _currentUserService.UserId;
            if (!userId.HasValue)
            {
                return Result.Failure<DocumentDto>(Error.Unauthorized);
            }

            var currentUser = await _userRepository.GetByIdWithRolesAsync(userId.Value, cancellationToken);
            if (currentUser == null)
            {
                return Result.Failure<DocumentDto>(Error.Unauthorized);
            }

            var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
            var isExecutive = roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin" or
                                             "TMD" or "TopManagingDirector" or "Country Manager" or
                                             "Deputy" or "DeputyDirector" or "Deputy Country Manager");
            if (!isExecutive)
            {
                var canView = document.CreatedById == userId.Value
                              || document.CurrentApproverId == userId.Value
                              || document.TargetUserId == userId.Value
                              || (currentUser.OrganizationUnitId.HasValue && document.TargetDepartmentId == currentUser.OrganizationUnitId.Value);

                if (!canView && currentUser.OrganizationUnitId.HasValue)
                {
                    var creator = await _userRepository.GetByIdAsync(document.CreatedById, cancellationToken);
                    canView = creator?.OrganizationUnitId == currentUser.OrganizationUnitId;
                }

                if (!canView)
                {
                    return Result.Failure<DocumentDto>(Error.Forbidden);
                }
            }

            var dto = new DocumentDto
            {
                Id = document.Id,
                DocumentNumber = document.DocumentNumber,
                Title = document.Title,
                Description = document.Description,
                Status = document.Status,
                CurrentVersion = document.CurrentVersion,
                DocumentTypeId = document.DocumentTypeId,
                CategoryId = document.CategoryId,
                CreatedById = document.CreatedById,
                CreatedAt = document.CreatedAt,
                LastModifiedAt = document.LastModifiedAt,
                EffectiveDate = document.EffectiveDate,
                ExpirationDate = document.ExpirationDate,
                Versions = document.Versions?.Select(v => new DocumentVersionDto
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    Content = v.Content,
                    ChangeNotes = v.ChangeNotes,
                    CreatedAt = v.CreatedAt
                }).ToList() ?? new List<DocumentVersionDto>(),
                Approvals = document.Approvals?.Select(a => new DocumentApprovalDto
                {
                    Id = a.Id,
                    ApproverId = a.ApproverId,
                    ApprovedAt = a.ApprovedAt,
                    Comments = a.Comments,
                    IsApproved = a.IsApproved
                }).ToList() ?? new List<DocumentApprovalDto>()
            };

            return Result.Success(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId}", request.DocumentId);
            return Result.Failure<DocumentDto>(Error.Custom("Document.QueryFailed", "Failed to retrieve document."));
        }
    }
}
