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

[Authorize(Permissions = Permissions.Tasks.Edit)]
public record RejectTaskCommand(Guid TaskId, string Remarks) : IRequest<Result>;

public class RejectTaskCommandHandler : IRequestHandler<RejectTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectTaskCommandHandler> _logger;

    public RejectTaskCommandHandler(
        ITaskRepository taskRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<RejectTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(RejectTaskCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Remarks))
                return Result.Failure(Error.Custom("Task.RemarksRequired", "Rejection remarks are required."));

            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;
            if (userId == null || tenantId == null)
                return Result.Failure(Error.Unauthorized);

            var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
            if (task == null)
                return Result.Failure(Error.NotFound);
            if (task.TenantId != tenantId)
                return Result.Failure(Error.Forbidden);

            if (task.Status != QmsTaskStatus.AwaitingApproval)
                return Result.Failure(Error.Custom("Task.StatusError", "Only tasks awaiting approval can be rejected."));

            task.Reject(request.Remarks);
            await _taskRepository.UpdateAsync(task, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "TASK_REJECTED",
                "Tasks",
                task.Id,
                $"Task '{task.Title}' was rejected. Reason: {request.Remarks}",
                cancellationToken);

            if (task.AssignedToId.HasValue)
            {
                await _notificationService.SendAsync(
                    task.AssignedToId.Value,
                    "Task Rejected",
                    $"Your task '{task.Title}' was rejected: {request.Remarks}",
                    NotificationType.TaskAssignment,
                    task.Id,
                    cancellationToken);
            }

            _logger.LogInformation("Task {TaskId} rejected by manager {UserId}", task.Id, userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting task {TaskId}", request.TaskId);
            return Result.Failure(Error.Custom("Task.RejectFailed", "Failed to reject task."));
        }
    }
}
