using KasahQMS.Domain.Common.Interfaces;

namespace KasahQMS.Domain.Events;

public record CapaCreatedEvent(Guid CapaId, string Title, string CapaType) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CapaStartedEvent(Guid CapaId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CapaVerifiedEvent(Guid CapaId, Guid VerifiedById, bool IsEffective) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record CapaClosedEvent(Guid CapaId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
