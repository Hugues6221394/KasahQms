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
public record ArchiveTaskCommand(Guid TaskId) : IRequest<Result>;

public class ArchiveTaskCommandHandler : IRequestHandler<ArchiveTaskCommand, Result>
{
    private readonly ITaskRepository _taskRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ArchiveTaskCommandHandler> _logger;

    public ArchiveTaskCommandHandler(
        ITaskRepository taskRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<ArchiveTaskCommandHandler> logger)
    {
        _taskRepository = taskRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ArchiveTaskCommand request, CancellationToken cancellationToken)
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

            // Only completed tasks can be archived
            if (task.Status != QmsTaskStatus.Completed)
                return Result.Failure(Error.Custom("Task.StatusError", "Only completed tasks can be archived."));

            task.Archive();
            await _taskRepository.UpdateAsync(task, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "TASK_ARCHIVED",
                "Tasks",
                task.Id,
                $"Task '{task.Title}' was archived",
                cancellationToken);

            _logger.LogInformation("Task {TaskId} archived by user {UserId}", task.Id, userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error archiving task {TaskId}", request.TaskId);
            return Result.Failure(Error.Custom("Task.ArchiveFailed", "Failed to archive task."));
        }
    }
}
