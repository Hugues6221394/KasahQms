using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// Organization unit entity for organizational structure.
/// </summary>
public class OrganizationUnit : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual OrganizationUnit? Parent { get; set; }
    public virtual ICollection<OrganizationUnit>? Children { get; set; }
    public virtual ICollection<User>? Users { get; set; }
    
    public OrganizationUnit() { }
    
    public static OrganizationUnit Create(
        Guid tenantId,
        string name,
        string code,
        string? description = null,
        Guid? parentId = null,
        Guid? createdById = null)
    {
        return new OrganizationUnit
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Code = code,
            Description = description,
            ParentId = parentId,
            IsActive = true,
            CreatedById = createdById ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow
        };
    }
}
