namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for audit logging.
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string action,
        string entityType,
        Guid? entityId,
        string? description,
        CancellationToken cancellationToken = default);

    Task LogAuthenticationAsync(
        Guid? userId,
        string action,
        string? description,
        string? ipAddress,
        string? userAgent,
        bool isSuccessful,
        CancellationToken cancellationToken = default);

    Task LogUserActivityAsync(
        Guid userId,
        string eventType,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceInfo = null,
        CancellationToken cancellationToken = default);
}
