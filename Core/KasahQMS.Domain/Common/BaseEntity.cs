using KasahQMS.Domain.Common.Interfaces;

namespace KasahQMS.Domain.Common;

/// <summary>
/// Base class for all domain entities.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    
    private readonly List<IDomainEvent> _domainEvents = new();
    
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }
    
    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }
    
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Base class for entities that require audit tracking.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? LastModifiedById { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}
