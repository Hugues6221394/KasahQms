using System.Text.Json;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Privacy;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Data privacy service for GDPR compliance including consent management,
/// data export, and user data anonymization.
/// </summary>
public class DataPrivacyService : IDataPrivacyService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DataPrivacyService> _logger;

    public DataPrivacyService(ApplicationDbContext dbContext, ILogger<DataPrivacyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Guid> RequestDataExportAsync(
        Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var request = new DataExportRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            RequestedAt = DateTime.UtcNow,
            Status = DataExportStatus.Pending
        };

        _dbContext.DataExportRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Data export requested for user {UserId}", userId);
        return request.Id;
    }

    public async Task ProcessDataExportAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await _dbContext.DataExportRequests
            .FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

        if (request == null) return;

        try
        {
            request.Status = DataExportStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
            {
                request.Status = DataExportStatus.Failed;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // The downloadable payload is generated on demand by the secure page handler.
            request.DownloadUrl = $"/Privacy/DataExport?handler=Download&id={request.Id}";
            request.Status = DataExportStatus.Completed;
            request.CompletedAt = DateTime.UtcNow;
            request.ExpiresAt = DateTime.UtcNow.AddDays(30);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Data export completed for request {RequestId}", requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data export failed for request {RequestId}", requestId);
            request.Status = DataExportStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RecordConsentAsync(
        Guid userId, Guid tenantId, ConsentType type, bool isGranted,
        string? ipAddress = null, string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        // Revoke any existing consent of this type first
        var existing = await _dbContext.ConsentRecords
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ConsentType == type && c.IsGranted, cancellationToken);

        if (existing != null)
        {
            existing.IsGranted = false;
            existing.RevokedAt = DateTime.UtcNow;
        }

        var record = new ConsentRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            ConsentType = type,
            IsGranted = isGranted,
            GrantedAt = isGranted ? DateTime.UtcNow : null,
            IpAddress = ipAddress,
            UserAgent = userAgent
        };

        _dbContext.ConsentRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeConsentAsync(
        Guid userId, ConsentType type, CancellationToken cancellationToken = default)
    {
        var consents = await _dbContext.ConsentRecords
            .Where(c => c.UserId == userId && c.ConsentType == type && c.IsGranted)
            .ToListAsync(cancellationToken);

        foreach (var consent in consents)
        {
            consent.IsGranted = false;
            consent.RevokedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ConsentRecord>> GetConsentsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ConsentRecords
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.GrantedAt ?? c.RevokedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AnonymizeUserDataAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null) return;

        // Replace PII with redacted values (GDPR right to erasure)
        user.Email = $"[REDACTED]_{userId:N}@redacted.local";
        user.FirstName = "[REDACTED]";
        user.LastName = "[REDACTED]";
        user.PhoneNumber = null;
        user.JobTitle = null;
        user.LastLoginIp = null;
        user.PasswordHistory = null;
        user.Deactivate();

        // Revoke all active sessions
        var sessions = await _dbContext.UserSessions
            .Where(s => s.UserId == userId && !s.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.Revoke();

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User data anonymized for user {UserId}", userId);
    }
}
