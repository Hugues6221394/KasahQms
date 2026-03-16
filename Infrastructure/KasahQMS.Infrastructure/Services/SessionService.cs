using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Security;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Session management service implementation.
/// </summary>
public class SessionService : ISessionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<SessionService> _logger;

    public SessionService(ApplicationDbContext dbContext, ILogger<SessionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserSession> CreateSessionAsync(
        Guid userId, Guid tenantId, string tokenHash,
        string? deviceInfo, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Token = tokenHash,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            LastActivityAt = DateTime.UtcNow
        };

        _dbContext.UserSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<List<UserSession>> GetActiveSessionsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastActivityAt)
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(
        Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.UserSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session != null)
        {
            session.Revoke();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.Revoke();

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CleanupExpiredSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        var expiredSessions = await _dbContext.UserSessions
            .Where(s => s.ExpiresAt <= DateTime.UtcNow && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in expiredSessions)
            session.Revoke();

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
        return expiredSessions.Count;
    }
}
