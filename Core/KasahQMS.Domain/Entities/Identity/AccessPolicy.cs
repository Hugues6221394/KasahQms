using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// Attribute-based policy rule assigned to a role for fine-grained access control.
/// </summary>
public class AccessPolicy : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? RoleId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Attribute { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public virtual Role? Role { get; set; }

    public AccessPolicy() { }

    public static AccessPolicy Create(
        Guid tenantId,
        string name,
        string scope,
        string attribute,
        string @operator,
        string value,
        Guid createdById,
        Guid? roleId = null,
        string? description = null)
    {
        return new AccessPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Scope = scope,
            Attribute = attribute,
            Operator = @operator,
            Value = value,
            RoleId = roleId,
            Description = description,
            IsActive = true,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }
}

