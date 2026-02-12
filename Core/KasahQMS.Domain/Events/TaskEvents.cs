using KasahQMS.Domain.Common.Interfaces;

namespace KasahQMS.Domain.Events;

public record TaskCreatedEvent(Guid TaskId, string Title) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TaskAssignedEvent(Guid TaskId, Guid AssignedToId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TaskCompletedEvent(Guid TaskId, Guid CompletedById) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record TaskOverdueEvent(Guid TaskId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
