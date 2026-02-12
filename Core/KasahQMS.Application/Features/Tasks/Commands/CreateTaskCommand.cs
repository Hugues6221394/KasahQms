using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Tasks.Commands;

[Authorize(Permissions = Permissions.Tasks.Create)]
public record CreateTaskCommand(
    string Title,
    string? Description,
    Guid? AssignedToId,
    DateTime? DueDate,
    TaskPriority Priority,
    Guid? LinkedDocumentId,
    Guid? LinkedCapaId,
    Guid? LinkedAuditId,
    List<Guid>? AssignedToUserIds = null,
    Guid? AssignedToOrgUnitId = null) : IRequest<Result<Guid>>;

public class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, Result<Guid>>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ITaskAssignmentRepository _taskAssignmentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateTaskCommandHandler> _logger;

    public CreateTaskCommandHandler(
        ITaskRepository taskRepository,
        ITaskAssignmentRepository taskAssignmentRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<CreateTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _taskAssignmentRepository = taskAssignmentRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (userId == null || tenantId == null)
            {
                return Result.Failure<Guid>(Error.Unauthorized);
            }

            // Generate task number
            var count = await _taskRepository.GetCountForYearAsync(tenantId.Value, DateTime.UtcNow.Year, cancellationToken);
            var taskNumber = $"TASK-{DateTime.UtcNow.Year}-{(count + 1):D5}";

            var task = QmsTask.Create(
                tenantId.Value,
                request.Title,
                taskNumber,
                userId.Value,
                request.Description,
                request.Priority,
                request.DueDate);

            var userIds = (request.AssignedToUserIds ?? new List<Guid>()).Distinct().ToList();
            var primary = request.AssignedToId ?? userIds.FirstOrDefault();
            if (primary != Guid.Empty)
            {
                task.Assign(primary);
            }

            if (request.AssignedToOrgUnitId.HasValue)
                task.AssignedToOrgUnitId = request.AssignedToOrgUnitId;

            if (request.LinkedDocumentId.HasValue)
                task.LinkToDocument(request.LinkedDocumentId.Value);

            if (request.LinkedCapaId.HasValue)
                task.LinkToCapa(request.LinkedCapaId.Value);

            if (request.LinkedAuditId.HasValue)
                task.LinkToAudit(request.LinkedAuditId.Value);

            await _taskRepository.AddAsync(task, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var assigneeIds = new HashSet<Guid>();
            if (primary != Guid.Empty)
                assigneeIds.Add(primary);
            foreach (var id in userIds)
                assigneeIds.Add(id);

            var extra = assigneeIds.Where(x => x != primary).ToList();
            if (extra.Count > 0)
            {
                var assignments = extra.Select(uid => new TaskAssignment
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    UserId = uid
                }).ToList();
                await _taskAssignmentRepository.AddRangeAsync(assignments, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            await _auditLogService.LogAsync(
                "TASK_CREATED",
                "Tasks",
                task.Id,
                $"Task '{request.Title}' created",
                cancellationToken);

            foreach (var assigneeId in assigneeIds)
            {
                await _notificationService.SendAsync(
                    assigneeId,
                    "New Task Assigned",
                    $"You have been assigned a new task: {request.Title}",
                    NotificationType.TaskAssignment,
                    task.Id,
                    cancellationToken);
            }

            _logger.LogInformation("Task {TaskId} created by user {UserId}", task.Id, userId);

            return Result.Success(task.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task");
            return Result.Failure<Guid>(Error.Custom("Task.CreateFailed", "Failed to create task."));
        }
    }
}
