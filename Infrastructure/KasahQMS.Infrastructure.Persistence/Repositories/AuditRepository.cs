using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for Audit entity.
/// </summary>
public class AuditRepository : BaseRepository<Audit>, IAuditRepository
{
    public AuditRepository(ApplicationDbContext context) : base(context) { }

    public async Task<Audit?> GetByIdWithFindingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.Findings)
            .Include(a => a.TeamMembers)
            .Include(a => a.LeadAuditor)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Audit?> GetByAuditNumberAsync(string auditNumber, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.AuditNumber == auditNumber && a.TenantId == tenantId, cancellationToken);
    }

    public async Task<(IEnumerable<Audit> Audits, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        string? searchTerm = null,
        string? status = null,
        string? auditType = null,
        string sortBy = "PlannedStartDate",
        bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(a => a.LeadAuditor)
            .Include(a => a.Findings)
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(a => 
                a.Title.Contains(searchTerm) || 
                a.AuditNumber.Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AuditStatus>(status, out var auditStatus))
        {
            query = query.Where(a => a.Status == auditStatus);
        }

        if (!string.IsNullOrEmpty(auditType) && Enum.TryParse<AuditType>(auditType, out var parsedAuditType))
        {
            query = query.Where(a => a.AuditType == parsedAuditType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = sortBy switch
        {
            "Title" => sortDescending ? query.OrderByDescending(a => a.Title) : query.OrderBy(a => a.Title),
            "Status" => sortDescending ? query.OrderByDescending(a => a.Status) : query.OrderBy(a => a.Status),
            "CreatedAt" => sortDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt),
            _ => sortDescending ? query.OrderByDescending(a => a.PlannedStartDate) : query.OrderBy(a => a.PlannedStartDate)
        };

        var audits = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (audits, totalCount);
    }

    public async Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(a => a.TenantId == tenantId && a.CreatedAt.Year == year)
            .CountAsync(cancellationToken);
    }

    public async Task<IEnumerable<Audit>> GetUpcomingAuditsAsync(Guid tenantId, int days, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var endDate = now.AddDays(days);
        
        return await _dbSet
            .Where(a => a.TenantId == tenantId && 
                        a.PlannedStartDate >= now && 
                        a.PlannedStartDate <= endDate &&
                        a.Status == AuditStatus.Planned)
            .OrderBy(a => a.PlannedStartDate)
            .ToListAsync(cancellationToken);
    }
}

