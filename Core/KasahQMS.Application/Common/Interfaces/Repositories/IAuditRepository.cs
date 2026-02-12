using KasahQMS.Domain.Entities.Audits;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for Audit entity operations.
/// </summary>
public interface IAuditRepository : IRepository<Audit>
{
    Task<Audit?> GetByIdWithFindingsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Audit?> GetByAuditNumberAsync(string auditNumber, Guid tenantId, CancellationToken cancellationToken = default);
    
    Task<(IEnumerable<Audit> Audits, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? status = null,
        string? auditType = null,
        string sortBy = "PlannedStartDate",
        bool sortDescending = true,
        CancellationToken cancellationToken = default);
    
    Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<Audit>> GetUpcomingAuditsAsync(Guid tenantId, int days, CancellationToken cancellationToken = default);
}
