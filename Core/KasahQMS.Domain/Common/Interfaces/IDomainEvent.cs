namespace KasahQMS.Domain.Common.Interfaces;

/// <summary>
/// Marker interface for domain events.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
