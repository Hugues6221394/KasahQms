using KasahQMS.Domain.Entities.Tasks;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

public interface ITaskAssignmentRepository
{
    Task AddRangeAsync(IEnumerable<TaskAssignment> assignments, CancellationToken cancellationToken = default);
}
