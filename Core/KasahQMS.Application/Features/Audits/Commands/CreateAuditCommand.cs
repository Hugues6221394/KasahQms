using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Notifications;
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
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAuditCommandHandler> _logger;

    public CreateAuditCommandHandler(
        IAuditRepository auditRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IEmailService emailService,
        IUnitOfWork unitOfWork,
        ILogger<CreateAuditCommandHandler> logger)
    {
        _auditRepository = auditRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _emailService = emailService;
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

            if (request.LeadAuditorId.HasValue)
            {
                await _notificationService.SendAsync(
                    request.LeadAuditorId.Value,
                    "Audit Scheduled",
                    $"You have been assigned as lead auditor for '{request.Title}' ({request.ScheduledStartDate:yyyy-MM-dd} to {request.ScheduledEndDate:yyyy-MM-dd}).",
                    NotificationType.AuditScheduled,
                    audit.Id,
                    cancellationToken);

                var leadAuditor = await _userRepository.GetByIdWithRolesAsync(request.LeadAuditorId.Value, cancellationToken);
                if (!string.IsNullOrWhiteSpace(leadAuditor?.Email))
                {
                    await _emailService.SendEmailAsync(
                        leadAuditor.Email!,
                        $"Audit Scheduled: {request.Title}",
                        $"<p>Hello {leadAuditor.FullName},</p>" +
                        $"<p>You have been assigned as <strong>Lead Auditor</strong>.</p>" +
                        $"<p><strong>Audit:</strong> {request.Title}<br/>" +
                        $"<strong>Type:</strong> {request.AuditType}<br/>" +
                        $"<strong>Schedule:</strong> {request.ScheduledStartDate:MMMM dd, yyyy} - {request.ScheduledEndDate:MMMM dd, yyyy}</p>" +
                        $"<p>Please log in to KASAH QMS to review the scope and prepare execution.</p>",
                        true,
                        cancellationToken);
                }
            }

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
