using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Document repository implementation.
/// </summary>
public class DocumentRepository : BaseRepository<Document>, IDocumentRepository
{
    public DocumentRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<Document?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.DocumentType)
            .Include(d => d.Category)
            .Include(d => d.Versions)
            .Include(d => d.Approvals)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Document?> GetByIdWithVersionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetByCreatorIdsAsync(IEnumerable<Guid> creatorIds, CancellationToken cancellationToken = default)
    {
        var ids = creatorIds.ToList();
        return await _dbSet
            .Where(d => ids.Contains(d.CreatedById))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetByApproverIdAsync(Guid approverId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.CurrentApproverId == approverId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Document>> GetByOrganizationUnitAsync(Guid organizationUnitId, CancellationToken cancellationToken = default)
    {
        // Get user IDs in the organization unit, then get their documents
        var userIds = await _context.Set<Domain.Entities.Identity.User>()
            .Where(u => u.OrganizationUnitId == organizationUnitId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        
        if (!userIds.Any())
        {
            return Enumerable.Empty<Document>();
        }
        
        return await _dbSet
            .Where(d => userIds.Contains(d.CreatedById))
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .CountAsync(d => d.TenantId == tenantId && d.CreatedAt.Year == year, cancellationToken);
    }
}
