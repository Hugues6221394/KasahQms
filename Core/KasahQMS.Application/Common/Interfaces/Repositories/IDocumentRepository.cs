using KasahQMS.Domain.Entities.Documents;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for Document entities.
/// </summary>
public interface IDocumentRepository : IRepository<Document>
{
    Task<Document?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdWithVersionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetByCreatorIdsAsync(IEnumerable<Guid> creatorIds, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetByApproverIdAsync(Guid approverId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Document>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default);
    Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);
}
