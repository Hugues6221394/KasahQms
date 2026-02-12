using KasahQMS.Domain.Entities.Tasks;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for QmsTask entities.
/// </summary>
public interface ITaskRepository : IRepository<QmsTask>
{
    Task<IEnumerable<QmsTask>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<QmsTask>> GetByAssigneeAsync(Guid assigneeId, CancellationToken cancellationToken = default);
    Task<IEnumerable<QmsTask>> GetByAssigneeIdsAsync(IEnumerable<Guid> assigneeIds, CancellationToken cancellationToken = default);
    /// <summary>Tasks where user is primary assignee, in TaskAssignment, or task assigned to user's org.</summary>
    Task<IEnumerable<QmsTask>> GetTasksForUserAsync(Guid tenantId, Guid userId, Guid? orgUnitId, CancellationToken cancellationToken = default);
    Task<IEnumerable<QmsTask>> GetOverdueTasksAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<int> GetCountForYearAsync(Guid tenantId, int year, CancellationToken cancellationToken = default);
}
