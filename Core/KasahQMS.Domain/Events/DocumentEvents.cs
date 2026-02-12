using KasahQMS.Domain.Common.Interfaces;

namespace KasahQMS.Domain.Events;

public record DocumentCreatedEvent(Guid DocumentId, string Title) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record DocumentSubmittedEvent(Guid DocumentId, Guid SubmittedById) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record DocumentApprovedEvent(Guid DocumentId, Guid ApprovedById) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record DocumentRejectedEvent(Guid DocumentId, Guid RejectedById, string Reason) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
