using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Audits.Commands;

[Authorize(Permissions = Permissions.Audits.Create)]
public record CreateAuditCommand(
    string Title,
    string? Description,
    AuditType AuditType,
    DateTime ScheduledStartDate,
    DateTime ScheduledEndDate,
    Guid? LeadAuditorId,
    string? Scope,
    string? Objectives) : IRequest<Result<Guid>>;

public class CreateAuditCommandHandler : IRequestHandler<CreateAuditCommand, Result<Guid>>
{
    private readonly IAuditRepository _auditRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAuditCommandHandler> _logger;

    public CreateAuditCommandHandler(
        IAuditRepository auditRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<CreateAuditCommandHandler> logger)
    {
        _auditRepository = auditRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<Guid>> Handle(CreateAuditCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (userId == null || tenantId == null)
            {
                return Result.Failure<Guid>(Error.Unauthorized);
            }

            // Generate audit number
            var count = await _auditRepository.GetCountForYearAsync(tenantId.Value, DateTime.UtcNow.Year, cancellationToken);
            var auditNumber = $"AUD-{DateTime.UtcNow.Year}-{(count + 1):D4}";

            var audit = Audit.Create(
                tenantId.Value,
                request.Title,
                auditNumber,
                request.AuditType,
                request.ScheduledStartDate,
                request.ScheduledEndDate,
                userId.Value);

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                audit.SetDescription(request.Description);
            }

            if (!string.IsNullOrWhiteSpace(request.Scope))
            {
                audit.SetScope(request.Scope);
            }

            if (!string.IsNullOrWhiteSpace(request.Objectives))
            {
                audit.SetObjectives(request.Objectives);
            }

            if (request.LeadAuditorId.HasValue)
            {
                audit.SetLeadAuditor(request.LeadAuditorId.Value);
            }

            await _auditRepository.AddAsync(audit, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "AUDIT_CREATED",
                "Audits",
                audit.Id,
                $"Audit '{request.Title}' created",
                cancellationToken);

            _logger.LogInformation("Audit {AuditId} created by user {UserId}", audit.Id, userId);

            return Result.Success(audit.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating audit");
            return Result.Failure<Guid>(Error.Custom("Audit.CreateFailed", "Failed to create audit."));
        }
    }
}
