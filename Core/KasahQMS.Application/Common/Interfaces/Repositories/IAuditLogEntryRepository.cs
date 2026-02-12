using KasahQMS.Domain.Entities.AuditLog;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for querying audit log entries.
/// </summary>
public interface IAuditLogEntryRepository
{
    Task<IEnumerable<AuditLogEntry>> GetFilteredAsync(
        Guid tenantId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? userId = null,
        string? actionType = null,
        string? entityType = null,
        bool? isSuccessful = null,
        CancellationToken cancellationToken = default);
}

