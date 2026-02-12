using KasahQMS.Domain.Entities.Capa;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for CAPA entity operations.
/// </summary>
public interface ICapaRepository : IRepository<Capa>
{
    Task<Capa?> GetByIdWithActionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Capa?> GetByCapaNumberAsync(string capaNumber, Guid tenantId, CancellationToken cancellationToken = default);
    
    Task<(IEnumerable<Capa> Capas, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? status = null,
        string? severity = null,
        Guid? ownerId = null,
        string sortBy = "CreatedAt",
        bool sortDescending = true,
        CancellationToken cancellationToken = default);
    
    Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);
    Task<IEnumerable<Capa>> GetOverdueAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Capa>> GetByAuditIdAsync(Guid auditId, CancellationToken cancellationToken = default);
}
