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
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetDocumentsQueryHandler> _logger;

    public GetDocumentsQueryHandler(
        IDocumentRepository documentRepository,
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ILogger<GetDocumentsQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _currentUserService = currentUserService;
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

            var currentUser = await _userRepository.GetByIdWithRolesAsync(userId.Value, cancellationToken);
            if (currentUser == null)
            {
                return Result.Failure<PaginatedList<DocumentListDto>>(Error.Unauthorized);
            }

            var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
            var isExecutive = roles.Any(r => r is "System Admin" or "SystemAdmin" or "Admin" or "TenantAdmin" or
                                             "TMD" or "TopManagingDirector" or "Country Manager" or
                                             "Deputy" or "DeputyDirector" or "Deputy Country Manager");
            var orgUnitId = currentUser.OrganizationUnitId;

            var tenantDocuments = await _documentRepository.GetAllForTenantAsync(tenantId.Value, cancellationToken);
            IEnumerable<Document> documents = tenantDocuments;

            if (!isExecutive)
            {
                var deptUserIds = orgUnitId.HasValue
                    ? (await _userRepository.GetUserIdsInOrganizationUnitAsync(orgUnitId.Value, cancellationToken)).ToHashSet()
                    : new HashSet<Guid>();

                documents = tenantDocuments.Where(d =>
                    d.CreatedById == userId.Value ||
                    d.CurrentApproverId == userId.Value ||
                    d.TargetUserId == userId.Value ||
                    (orgUnitId.HasValue && d.TargetDepartmentId == orgUnitId.Value) ||
                    (orgUnitId.HasValue && deptUserIds.Contains(d.CreatedById)));
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
