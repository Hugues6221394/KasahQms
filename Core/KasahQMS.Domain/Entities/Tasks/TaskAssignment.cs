using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Tasks;

/// <summary>
/// Assigns a user to a task. Used for multi-assign (TMD/Deputy).
/// AssignedToId on QmsTask remains as primary assignee; this stores additional assignees.
/// </summary>
public class TaskAssignment
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }

    public virtual QmsTask? Task { get; set; }
    public virtual User? User { get; set; }
}
