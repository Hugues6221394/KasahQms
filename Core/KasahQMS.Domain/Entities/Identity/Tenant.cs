using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// Tenant entity for multi-tenancy support.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? DeactivatedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<OrganizationUnit>? OrganizationUnits { get; set; }
    public virtual ICollection<User>? Users { get; set; }
    
    public Tenant() { }
    
    public static Tenant Create(string name, string code, string? description = null)
    {
        return new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Code = code,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void Activate() => IsActive = true;
    
    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
    }
}
