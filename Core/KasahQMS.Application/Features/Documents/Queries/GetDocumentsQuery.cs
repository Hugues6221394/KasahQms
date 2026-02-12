using System.Linq;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Models;
using KasahQMS.Application.Common.Security;
using KasahQMS.Application.Features.Documents.Dtos;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Documents.Queries;

[Authorize(Permissions = $"{Permissions.Documents.View}, {Permissions.Documents.ViewAll}")]
public record GetDocumentsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    DocumentStatus? Status = null,
    Guid? DocumentTypeId = null,
    Guid? CategoryId = null) : IRequest<Result<PaginatedList<DocumentListDto>>>;

public class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, Result<PaginatedList<DocumentListDto>>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetDocumentsQueryHandler> _logger;

    public GetDocumentsQueryHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IAuthorizationService authorizationService,
        IUserRepository userRepository,
        ILogger<GetDocumentsQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _authorizationService = authorizationService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<PaginatedList<DocumentListDto>>> Handle(
        GetDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null)
            {
                return Result.Failure<PaginatedList<DocumentListDto>>(Error.Unauthorized);
            }

            // Check if user has ViewAll permission (TMD, Deputy, Managers)
            var hasViewAll = await _authorizationService.HasPermissionAsync(
                Permissions.Documents.ViewAll, 
                cancellationToken);

            IEnumerable<Document> documents;

            if (hasViewAll)
            {
                // User can view all documents - get based on hierarchy
                var visibleUserIds = await _hierarchyService.GetVisibleUserIdsAsync(userId.Value, cancellationToken);
                documents = await _documentRepository.GetByCreatorIdsAsync(visibleUserIds, cancellationToken);
                
                // Also include documents awaiting their approval
                var approvalDocuments = await _documentRepository.GetByApproverIdAsync(userId.Value, cancellationToken);
                documents = documents.Union(approvalDocuments).DistinctBy(d => d.Id);
            }
            else
            {
                // User can only view their own documents and documents assigned to them
                var myDocuments = await _documentRepository.GetByCreatorIdsAsync(new[] { userId.Value }, cancellationToken);
                var approvalDocuments = await _documentRepository.GetByApproverIdAsync(userId.Value, cancellationToken);
                documents = myDocuments.Union(approvalDocuments).DistinctBy(d => d.Id);
            }

            // Apply filters - also filter by tenant for security
            var query = documents
                .Where(d => d.TenantId == tenantId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(d =>
                    d.Title.ToLower().Contains(term) ||
                    d.DocumentNumber.ToLower().Contains(term));
            }

            if (request.Status.HasValue)
            {
                query = query.Where(d => d.Status == request.Status.Value);
            }

            if (request.DocumentTypeId.HasValue)
            {
                query = query.Where(d => d.DocumentTypeId == request.DocumentTypeId.Value);
            }

            if (request.CategoryId.HasValue)
            {
                query = query.Where(d => d.CategoryId == request.CategoryId.Value);
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(d => new DocumentListDto
                {
                    Id = d.Id,
                    DocumentNumber = d.DocumentNumber,
                    Title = d.Title,
                    Status = d.Status,
                    CurrentVersion = d.CurrentVersion,
                    CreatedAt = d.CreatedAt
                })
                .ToList();

            var result = new PaginatedList<DocumentListDto>(
                items,
                totalCount,
                request.PageNumber,
                request.PageSize);

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents");
            return Result.Failure<PaginatedList<DocumentListDto>>(
                Error.Custom("Documents.QueryFailed", "Failed to retrieve documents."));
        }
    }
}
