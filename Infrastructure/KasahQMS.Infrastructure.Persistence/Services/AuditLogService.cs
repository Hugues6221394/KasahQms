using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Domain.Entities.Security;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Persistence.Services;

/// <summary>
/// Audit log service implementation.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<AuditLogService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        Guid? entityId,
        string? description,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = AuditLogEntry.Create(
                action,
                entityType,
                entityId,
                description,
                _currentUserService.UserId,
                _currentUserService.TenantId,
                _currentUserService.IpAddress,
                _currentUserService.UserAgent);

            _dbContext.Set<AuditLogEntry>().Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit entry: {Action} on {EntityType}", action, entityType);
        }
    }

    public async Task LogAuthenticationAsync(
        Guid? userId,
        string action,
        string? description,
        string? ipAddress,
        string? userAgent,
        bool isSuccessful,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = AuditLogEntry.CreateAuthenticationLog(
                userId,
                action,
                description,
                ipAddress,
                userAgent,
                isSuccessful);

            _dbContext.Set<AuditLogEntry>().Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log authentication event: {Action}", action);
        }
    }

    public async Task LogUserActivityAsync(
        Guid userId,
        string eventType,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceInfo = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = _currentUserService.TenantId ?? await _dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => u.TenantId)
                .FirstOrDefaultAsync(cancellationToken);

            var activity = UserLoginActivity.Create(
                tenantId,
                userId,
                eventType,
                ipAddress ?? _currentUserService.IpAddress,
                userAgent ?? _currentUserService.UserAgent,
                deviceInfo);

            _dbContext.UserLoginActivities.Add(activity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log user activity {EventType} for user {UserId}", eventType, userId);
        }
    }
}
