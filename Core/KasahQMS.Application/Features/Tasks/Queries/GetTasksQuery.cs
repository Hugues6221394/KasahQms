using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Models;
using KasahQMS.Application.Common.Security;
using KasahQMS.Application.Features.Tasks.Dtos;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Tasks.Queries;

[Authorize(Permissions = $"{Permissions.Tasks.View}, {Permissions.Tasks.ViewAll}")]
public record GetTasksQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null,
    QmsTaskStatus? Status = null,
    Guid? AssignedToId = null) : IRequest<Result<PaginatedList<TaskDto>>>;

public class GetTasksQueryHandler : IRequestHandler<GetTasksQuery, Result<PaginatedList<TaskDto>>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<GetTasksQueryHandler> _logger;

    public GetTasksQueryHandler(
        ITaskRepository taskRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IAuthorizationService authorizationService,
        ILogger<GetTasksQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<Result<PaginatedList<TaskDto>>> Handle(
        GetTasksQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null)
            {
                return Result.Failure<PaginatedList<TaskDto>>(Error.Unauthorized);
            }

            // Check if user has ViewAll permission (TMD, Deputy, Managers)
            var hasViewAll = await _authorizationService.HasPermissionAsync(
                Permissions.Tasks.ViewAll, 
                cancellationToken);

            IEnumerable<Domain.Entities.Tasks.QmsTask> tasks;

            if (hasViewAll)
            {
                var visibleUserIds = await _hierarchyService.GetVisibleUserIdsAsync(userId.Value, cancellationToken);
                tasks = await _taskRepository.GetByAssigneeIdsAsync(visibleUserIds, cancellationToken);
            }
            else
            {
                var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
                var orgUnitId = user?.OrganizationUnitId;
                tasks = await _taskRepository.GetTasksForUserAsync(tenantId.Value, userId.Value, orgUnitId, cancellationToken);
            }

            // Also filter by tenant for security
            tasks = tasks.Where(t => t.TenantId == tenantId.Value);
            
            var query = tasks.AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var term = request.SearchTerm.ToLower();
                query = query.Where(t =>
                    t.Title.ToLower().Contains(term) ||
                    t.TaskNumber.ToLower().Contains(term));
            }

            if (request.Status.HasValue)
            {
                query = query.Where(t => t.Status == request.Status.Value);
            }

            if (request.AssignedToId.HasValue)
            {
                query = query.Where(t => t.AssignedToId == request.AssignedToId.Value);
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(t => new TaskDto
                {
                    Id = t.Id,
                    TaskNumber = t.TaskNumber,
                    Title = t.Title,
                    Description = t.Description,
                    Status = t.Status,
                    Priority = t.Priority,
                    AssignedToId = t.AssignedToId,
                    DueDate = t.DueDate,
                    CompletedAt = t.CompletedAt,
                    CreatedAt = t.CreatedAt
                })
                .ToList();

            var result = new PaginatedList<TaskDto>(items, totalCount, request.PageNumber, request.PageSize);
            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tasks");
            return Result.Failure<PaginatedList<TaskDto>>(
                Error.Custom("Tasks.QueryFailed", "Failed to retrieve tasks."));
        }
    }
}
