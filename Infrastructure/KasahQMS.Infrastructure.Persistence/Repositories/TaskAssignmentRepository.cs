using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Infrastructure.Persistence.Data;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

public class TaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly ApplicationDbContext _db;

    public TaskAssignmentRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AddRangeAsync(IEnumerable<TaskAssignment> assignments, CancellationToken cancellationToken = default)
    {
        await _db.TaskAssignments.AddRangeAsync(assignments, cancellationToken);
    }
}
