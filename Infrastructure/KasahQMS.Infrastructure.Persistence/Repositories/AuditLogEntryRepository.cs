using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.AuditLog;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for AuditLogEntry queries.
/// </summary>
public class AuditLogEntryRepository : IAuditLogEntryRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AuditLogEntryRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<AuditLogEntry>> GetFilteredAsync(
        Guid tenantId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? userId = null,
        string? actionType = null,
        string? entityType = null,
        bool? isSuccessful = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogEntries
            .Include(ale => ale.User)
            .Where(ale => ale.TenantId == tenantId)
            .AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(ale => ale.Timestamp >= startDate.Value.ToUniversalTime());
        }

        if (endDate.HasValue)
        {
            query = query.Where(ale => ale.Timestamp <= endDate.Value.ToUniversalTime().AddDays(1)); // Include full end day
        }

        if (userId.HasValue)
        {
            query = query.Where(ale => ale.UserId == userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(ale => ale.Action.Contains(actionType));
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(ale => ale.EntityType.Contains(entityType));
        }

        if (isSuccessful.HasValue)
        {
            query = query.Where(ale => ale.IsSuccessful == isSuccessful.Value);
        }

        return await query
            .OrderByDescending(ale => ale.Timestamp)
            .ToListAsync(cancellationToken);
    }
}

