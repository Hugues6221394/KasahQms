using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Capa;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for CAPA entity.
/// </summary>
public class CapaRepository : BaseRepository<Capa>, ICapaRepository
{
    public CapaRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Capa?> GetByIdWithActionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Actions)
            .Include(c => c.Owner)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Capa?> GetByCapaNumberAsync(string capaNumber, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.CapaNumber == capaNumber && c.TenantId == tenantId, cancellationToken);
    }

    public async Task<(IEnumerable<Capa> Capas, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? status = null,
        string? priority = null,
        Guid? ownerId = null,
        string sortBy = "CreatedAt",
        bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(c => c.Owner)
            .Include(c => c.Actions)
            .Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(c => 
                c.Title.Contains(searchTerm) || 
                c.CapaNumber.Contains(searchTerm) ||
                (c.Description != null && c.Description.Contains(searchTerm)));
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<CapaStatus>(status, out var capaStatus))
        {
            query = query.Where(c => c.Status == capaStatus);
        }

        if (!string.IsNullOrEmpty(priority) && Enum.TryParse<CapaPriority>(priority, out var capaPriority))
        {
            query = query.Where(c => c.Priority == capaPriority);
        }

        if (ownerId.HasValue)
        {
            query = query.Where(c => c.OwnerId == ownerId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy switch
        {
            "Title" => sortDescending ? query.OrderByDescending(c => c.Title) : query.OrderBy(c => c.Title),
            "Status" => sortDescending ? query.OrderByDescending(c => c.Status) : query.OrderBy(c => c.Status),
            "Priority" => sortDescending ? query.OrderByDescending(c => c.Priority) : query.OrderBy(c => c.Priority),
            "TargetCompletionDate" => sortDescending ? query.OrderByDescending(c => c.TargetCompletionDate) : query.OrderBy(c => c.TargetCompletionDate),
            _ => sortDescending ? query.OrderByDescending(c => c.CreatedAt) : query.OrderBy(c => c.CreatedAt)
        };

        var capas = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (capas, totalCount);
    }

    public async Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.TenantId == tenantId && c.CreatedAt.Year == year)
            .CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<Capa>> GetOverdueAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(c => c.TenantId == tenantId && 
                        c.TargetCompletionDate < now && 
                        c.Status != CapaStatus.Closed &&
                        c.Status != CapaStatus.EffectivenessVerified)
            .OrderBy(c => c.TargetCompletionDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Capa>> GetByAuditIdAsync(Guid auditId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.SourceAuditId == auditId)
            .ToListAsync(cancellationToken);
    }
}
