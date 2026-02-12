using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Capa.Commands;

[Authorize(Permissions = Permissions.Capa.Create)]
public record CreateCapaCommand(
    string Title,
    string? Description,
    CapaType CapaType,
    CapaPriority Priority,
    Guid? OwnerId,
    Guid? LinkedAuditId,
    Guid? LinkedAuditFindingId,
    DateTime? TargetCompletionDate,
    string? ImmediateActions) : IRequest<Result<Guid>>;

public class CreateCapaCommandHandler : IRequestHandler<CreateCapaCommand, Result<Guid>>
{
    private readonly ICapaRepository _capaRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateCapaCommandHandler> _logger;

    public CreateCapaCommandHandler(
        ICapaRepository capaRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        ILogger<CreateCapaCommandHandler> logger)
    {
        _capaRepository = capaRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateCapaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (userId == null || tenantId == null)
            {
                return Result.Failure<Guid>(Error.Unauthorized);
            }

            // Generate CAPA number
            var count = await _capaRepository.GetCountForYearAsync(tenantId.Value, DateTime.UtcNow.Year, cancellationToken);
            var capaNumber = $"CAPA-{DateTime.UtcNow.Year}-{(count + 1):D4}";

            var capa = Domain.Entities.Capa.Capa.Create(
                tenantId.Value,
                request.Title,
                capaNumber,
                request.CapaType,
                request.Priority,
                userId.Value);

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                capa.SetDescription(request.Description);
            }

            if (request.OwnerId.HasValue)
            {
                capa.AssignOwner(request.OwnerId.Value);
            }

            if (request.LinkedAuditId.HasValue)
            {
                capa.LinkToAudit(request.LinkedAuditId.Value);
            }

            if (request.LinkedAuditFindingId.HasValue)
            {
                capa.LinkToAuditFinding(request.LinkedAuditFindingId.Value);
            }

            if (request.TargetCompletionDate.HasValue)
            {
                capa.SetTargetCompletionDate(request.TargetCompletionDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.ImmediateActions))
            {
                capa.SetImmediateActions(request.ImmediateActions);
            }

            await _capaRepository.AddAsync(capa, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "CAPA_CREATED",
                "CAPA",
                capa.Id,
                $"CAPA '{request.Title}' created",
                cancellationToken);

            // Notify owner
            if (request.OwnerId.HasValue)
            {
                await _notificationService.SendAsync(
                    request.OwnerId.Value,
                    "CAPA Assigned",
                    $"You have been assigned as owner for CAPA: {request.Title}",
                    NotificationType.CapaAssignment,
                    capa.Id,
                    cancellationToken);
            }

            _logger.LogInformation("CAPA {CapaId} created by user {UserId}", capa.Id, userId);

            return Result.Success(capa.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating CAPA");
            return Result.Failure<Guid>(Error.Custom("Capa.CreateFailed", "Failed to create CAPA."));
        }
    }
}
