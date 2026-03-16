using System.ComponentModel.DataAnnotations;
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
/// Base class for entities that require audit tracking and optimistic concurrency.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? LastModifiedById { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    /// <summary>
    /// Optimistic concurrency token. Updated automatically by EF Core on each save.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }

    /// <summary>
    /// Soft delete flag for compliance and audit trail.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Timestamp when entity was soft deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// ID of user who performed the soft delete.
    /// </summary>
    public Guid? DeletedById { get; set; }
}
