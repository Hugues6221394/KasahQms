using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Tasks.Commands;

[Authorize(Permissions = Permissions.Tasks.Edit)]
public record DeleteTaskCommand(Guid TaskId) : IRequest<Result>;

public class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteTaskCommandHandler> _logger;

    public DeleteTaskCommandHandler(
        ITaskRepository taskRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<DeleteTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
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

        if (task.Status == QmsTaskStatus.Completed)
            return Result.Failure(Error.Conflict);

        await _taskRepository.DeleteAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditLogService.LogAsync("TASK_DELETED", "Tasks", request.TaskId, $"Task '{task.Title}' deleted", cancellationToken);
        _logger.LogInformation("Task {TaskId} deleted by user {UserId}", request.TaskId, userId);
        return Result.Success();
    }
}
