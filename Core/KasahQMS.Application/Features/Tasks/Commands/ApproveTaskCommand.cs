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

[Authorize(Permissions = Permissions.Tasks.Edit)] // Managers should have edit permission
public record ApproveTaskCommand(Guid TaskId) : IRequest<Result>;

public class ApproveTaskCommandHandler : IRequestHandler<ApproveTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveTaskCommandHandler> _logger;

    public ApproveTaskCommandHandler(
        ITaskRepository taskRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<ApproveTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ApproveTaskCommand request, CancellationToken cancellationToken)
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

            // Logic check: Only AwaitingApproval tasks can be approved
            if (task.Status != QmsTaskStatus.AwaitingApproval)
                return Result.Failure(Error.Custom("Task.StatusError", "Only tasks awaiting approval can be approved."));

            task.Approve();
            await _taskRepository.UpdateAsync(task, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "TASK_APPROVED",
                "Tasks",
                task.Id,
                $"Task '{task.Title}' was approved by manager",
                cancellationToken);

            if (task.AssignedToId.HasValue)
            {
                await _notificationService.SendAsync(
                    task.AssignedToId.Value,
                    "Task Approved",
                    $"Your task '{task.Title}' has been approved.",
                    NotificationType.TaskAssignment,
                    task.Id,
                    cancellationToken);
            }

            _logger.LogInformation("Task {TaskId} approved by manager {UserId}", task.Id, userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving task {TaskId}", request.TaskId);
            return Result.Failure(Error.Custom("Task.ApproveFailed", "Failed to approve task."));
        }
    }
}
