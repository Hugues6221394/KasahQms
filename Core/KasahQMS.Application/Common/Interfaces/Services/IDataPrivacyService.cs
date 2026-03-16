using KasahQMS.Domain.Entities.Privacy;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for data privacy operations including GDPR compliance.
/// </summary>
public interface IDataPrivacyService
{
    Task<Guid> RequestDataExportAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task ProcessDataExportAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task RecordConsentAsync(Guid userId, Guid tenantId, ConsentType type, bool isGranted, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);
    Task RevokeConsentAsync(Guid userId, ConsentType type, CancellationToken cancellationToken = default);
    Task<List<ConsentRecord>> GetConsentsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymizes all PII for a user (GDPR right to erasure).
    /// </summary>
    Task AnonymizeUserDataAsync(Guid userId, CancellationToken cancellationToken = default);
}
