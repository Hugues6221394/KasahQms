using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Application.Features.Tasks.Dtos;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Tasks.Queries;

/// <summary>
/// Query to get the current user's tasks (dashboard view).
/// Note: Users can always view their own tasks, so authorization is handled in the handler.
/// </summary>
public record GetMyTasksQuery : IRequest<Result<MyTasksDto>>
{
    public bool IncludeCompleted { get; init; } = false;
    public int Limit { get; init; } = 10;
}

public class MyTasksDto
{
    public List<TaskDto> OverdueTasks { get; set; } = new();
    public List<TaskDto> DueTodayTasks { get; set; } = new();
    public List<TaskDto> UpcomingTasks { get; set; } = new();
    public List<TaskDto> RecentlyCompletedTasks { get; set; } = new();
    public int TotalOpen { get; set; }
    public int TotalInProgress { get; set; }
    public int TotalOverdue { get; set; }
    public int TotalCompleted { get; set; }
}

public class GetMyTasksQueryHandler : IRequestHandler<GetMyTasksQuery, Result<MyTasksDto>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDateTimeService _dateTimeService;
    private readonly ILogger<GetMyTasksQueryHandler> _logger;

    public GetMyTasksQueryHandler(
        ITaskRepository taskRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IAuthorizationService authorizationService,
        IDateTimeService dateTimeService,
        ILogger<GetMyTasksQueryHandler> logger)
    {
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _authorizationService = authorizationService;
        _dateTimeService = dateTimeService;
        _logger = logger;
    }

    public async Task<Result<MyTasksDto>> Handle(GetMyTasksQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var userId = _currentUserService.UserId;
            if (tenantId == null || userId == null)
            {
                return Result.Failure<MyTasksDto>(Error.Unauthorized);
            }

            // Users can always view their own tasks - no authorization check needed
            // This is a "my tasks" query, so it's inherently scoped to the current user

            var hasViewAll = await _authorizationService.HasPermissionAsync(
                Permissions.Tasks.ViewAll, 
                cancellationToken);

            IEnumerable<Domain.Entities.Tasks.QmsTask> tasks;
            Guid? orgUnitId = null;

            if (hasViewAll)
            {
                var visibleUserIds = await _hierarchyService.GetVisibleUserIdsAsync(userId.Value, cancellationToken);
                tasks = await _taskRepository.GetByAssigneeIdsAsync(visibleUserIds, cancellationToken);
            }
            else
            {
                var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
                orgUnitId = user?.OrganizationUnitId;
                tasks = await _taskRepository.GetTasksForUserAsync(tenantId.Value, userId.Value, orgUnitId, cancellationToken);
            }

            // Filter by tenant for security
            tasks = tasks.Where(t => t.TenantId == tenantId.Value);
            var now = _dateTimeService.UtcNow;
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var nextWeek = today.AddDays(7);

            var taskList = tasks.ToList();

            // Categorize tasks
            var overdueTasks = taskList
                .Where(t => t.DueDate < today && t.Status != QmsTaskStatus.Completed && t.Status != QmsTaskStatus.Cancelled)
                .OrderBy(t => t.DueDate)
                .Take(request.Limit)
                .ToList();

            var dueTodayTasks = taskList
                .Where(t => t.DueDate >= today && t.DueDate < tomorrow && t.Status != QmsTaskStatus.Completed && t.Status != QmsTaskStatus.Cancelled)
                .OrderBy(t => t.DueDate)
                .Take(request.Limit)
                .ToList();

            var upcomingTasks = taskList
                .Where(t => t.DueDate >= tomorrow && t.DueDate <= nextWeek && t.Status != QmsTaskStatus.Completed && t.Status != QmsTaskStatus.Cancelled)
                .OrderBy(t => t.DueDate)
                .Take(request.Limit)
                .ToList();

            var recentlyCompleted = request.IncludeCompleted
                ? taskList
                    .Where(t => t.Status == QmsTaskStatus.Completed && t.CompletedAt >= today.AddDays(-7))
                    .OrderByDescending(t => t.CompletedAt)
                    .Take(request.Limit)
                    .ToList()
                : new List<Domain.Entities.Tasks.QmsTask>();

            var result = new MyTasksDto
            {
                OverdueTasks = overdueTasks.Select(t => MapToDto(t)).ToList(),
                DueTodayTasks = dueTodayTasks.Select(t => MapToDto(t)).ToList(),
                UpcomingTasks = upcomingTasks.Select(t => MapToDto(t)).ToList(),
                RecentlyCompletedTasks = recentlyCompleted.Select(t => MapToDto(t)).ToList(),
                TotalOpen = taskList.Count(t => t.Status == QmsTaskStatus.Open),
                TotalInProgress = taskList.Count(t => t.Status == QmsTaskStatus.InProgress),
                TotalOverdue = taskList.Count(t => t.Status == QmsTaskStatus.Overdue || (t.DueDate < now && t.Status != QmsTaskStatus.Completed && t.Status != QmsTaskStatus.Cancelled)),
                TotalCompleted = taskList.Count(t => t.Status == QmsTaskStatus.Completed)
            };

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get user's tasks");
            return Result.Failure<MyTasksDto>(Error.Custom("Tasks.QueryFailed", "An error occurred while retrieving your tasks."));
        }
    }

    private TaskDto MapToDto(Domain.Entities.Tasks.QmsTask task)
    {
        return new TaskDto
        {
            Id = task.Id,
            TaskNumber = task.TaskNumber,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            AssignedToId = task.AssignedToId,
            CompletedAt = task.CompletedAt,
            CreatedAt = task.CreatedAt
        };
    }
}
