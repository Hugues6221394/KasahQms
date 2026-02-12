using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Tasks.Commands;

[Authorize(Permissions = Permissions.Tasks.Complete)]
public record CompleteTaskCommand(Guid TaskId, string? CompletionNotes) : IRequest<Result>;

public class CompleteTaskCommandHandler : IRequestHandler<CompleteTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteTaskCommandHandler> _logger;

    public CompleteTaskCommandHandler(
        ITaskRepository taskRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUserRepository userRepository,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<CompleteTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;
            if (userId == null || tenantId == null)
                return Result.Failure(Error.Unauthorized);

            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
                return Result.Failure(Error.NotFound);
            if (task.TenantId != tenantId)
                return Result.Failure(Error.Forbidden);

            if (task.AssignedToId != userId.Value)
                return Result.Failure(Error.Forbidden);

            if (task.Status == QmsTaskStatus.Completed || task.Status == QmsTaskStatus.Cancelled)
                return Result.Failure(Error.Conflict);

            task.Complete(userId.Value, request.CompletionNotes);
            await _taskRepository.UpdateAsync(task, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "TASK_COMPLETED",
                "Tasks",
                task.Id,
                $"Task '{task.Title}' completed",
                cancellationToken);

            var creator = await _userRepository.GetByIdAsync(task.CreatedById, cancellationToken);
            if (creator?.ManagerId != null)
            {
                try
                {
                    await _notificationService.SendAsync(
                        creator.ManagerId.Value,
                        "Task completed",
                        $"Task '{task.Title}' was completed by assignee.",
                        NotificationType.TaskAssignment,
                        task.Id,
                        cancellationToken);
                }
                catch
                {
                    // Non-fatal
                }
            }

            _logger.LogInformation("Task {TaskId} completed by user {UserId}", task.Id, userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task {TaskId}", request.TaskId);
            return Result.Failure(Error.Custom("Task.CompleteFailed", "Failed to complete task."));
        }
    }
}
