using KasahQMS.Domain.Entities.Security;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for managing user sessions with device and location tracking.
/// </summary>
public interface ISessionService
{
    Task<UserSession> CreateSessionAsync(Guid userId, Guid tenantId, string tokenHash, string? deviceInfo, string? ipAddress, string? userAgent, CancellationToken cancellationToken = default);
    Task<List<UserSession>> GetActiveSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredSessionsAsync(CancellationToken cancellationToken = default);
}
