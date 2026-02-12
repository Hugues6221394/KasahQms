using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Task repository implementation.
/// </summary>
public class TaskRepository : BaseRepository<QmsTask>, ITaskRepository
{
    public TaskRepository(ApplicationDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<IEnumerable<QmsTask>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<QmsTask>> GetByAssigneeAsync(Guid assigneeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.AssignedToId == assigneeId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<QmsTask>> GetOverdueTasksAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(t => t.TenantId == tenantId &&
                        t.DueDate < now &&
                        t.Status != QmsTaskStatus.Completed &&
                        t.Status != QmsTaskStatus.Cancelled)
            .OrderBy(t => t.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<QmsTask>> GetByAssigneeIdsAsync(IEnumerable<Guid> assigneeIds, CancellationToken cancellationToken = default)
    {
        var ids = assigneeIds.ToList();
        return await _dbSet
            .Where(t => t.AssignedToId.HasValue && ids.Contains(t.AssignedToId.Value))
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<QmsTask>> GetTasksForUserAsync(Guid tenantId, Guid userId, Guid? orgUnitId, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                (t.AssignedToId == userId ||
                 _context.TaskAssignments.Any(a => a.TaskId == t.Id && a.UserId == userId) ||
                 (orgUnitId.HasValue && t.AssignedToOrgUnitId == orgUnitId)));
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .CountAsync(t => t.TenantId == tenantId && t.CreatedAt.Year == year, cancellationToken);
    }
}
