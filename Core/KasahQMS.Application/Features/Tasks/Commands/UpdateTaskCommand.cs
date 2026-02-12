using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Tasks.Commands;

[Authorize(Permissions = Permissions.Tasks.Edit)]
public record UpdateTaskCommand(
    Guid TaskId,
    string Title,
    string? Description,
    Guid? AssignedToId,
    DateTime? DueDate,
    TaskPriority Priority) : IRequest<Result>;

public class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateTaskCommandHandler> _logger;

    public UpdateTaskCommandHandler(
        ITaskRepository taskRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<UpdateTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null)
            return Result.Failure(Error.Unauthorized);

        var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken);
        if (task == null) return Result.Failure(Error.NotFound);
        if (task.TenantId != tenantId) return Result.Failure(Error.Forbidden);

        if (task.CreatedById != userId.Value)
            return Result.Failure(Error.Forbidden);

        if (task.Status == QmsTaskStatus.Completed || task.Status == QmsTaskStatus.Cancelled)
            return Result.Failure(Error.Conflict);

        task.Title = request.Title;
        task.Description = request.Description;
        task.DueDate = request.DueDate;
        task.Priority = request.Priority;
        if (request.AssignedToId.HasValue)
            task.Assign(request.AssignedToId.Value);
        else
            task.AssignedToId = null;

        await _taskRepository.UpdateAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("TASK_UPDATED", "Tasks", task.Id, $"Task '{task.Title}' updated", cancellationToken);
        _logger.LogInformation("Task {TaskId} updated by user {UserId}", task.Id, userId);
        return Result.Success();
    }
}
