using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// Tracks permission delegations from managers to subordinates.
/// Managers can only delegate permissions they themselves possess.
/// </summary>
public class UserPermissionDelegation : AuditableEntity
{
    public Guid UserId { get; set; }  // User receiving the delegated permission
    public Guid DelegatedById { get; set; }  // Manager who delegated the permission
    public string Permission { get; set; } = string.Empty;  // Permission string (e.g., "Documents.Create")
    public DateTime DelegatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }  // Optional expiration
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual User? User { get; set; }
    public virtual User? DelegatedBy { get; set; }
    
    public UserPermissionDelegation() { }
    
    public static UserPermissionDelegation Create(
        Guid tenantId,
        Guid userId,
        Guid delegatedById,
        string permission,
        int? expiresAfterDays = null,
        Guid? createdById = null)
    {
        return new UserPermissionDelegation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            DelegatedById = delegatedById,
            Permission = permission,
            DelegatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAfterDays.HasValue 
                ? DateTime.UtcNow.AddDays(expiresAfterDays.Value) 
                : null,
            IsActive = true,
            CreatedById = createdById ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void Deactivate()
    {
        IsActive = false;
        LastModifiedAt = DateTime.UtcNow;
    }
    
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    
    public bool IsValid => IsActive && !IsExpired;
}

