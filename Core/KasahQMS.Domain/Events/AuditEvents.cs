using KasahQMS.Domain.Common.Interfaces;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Events;

public record AuditCreatedEvent(Guid AuditId, string Title, string AuditType) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record AuditStartedEvent(Guid AuditId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record AuditCompletedEvent(Guid AuditId) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}

public record AuditFindingAddedEvent(
    Guid AuditId, 
    Guid FindingId, 
    string Title, 
    string FindingType) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
