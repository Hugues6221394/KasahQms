using KasahQMS.Domain.Common;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// Role entity for role-based access control.
/// </summary>
public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public Permission[] Permissions { get; set; } = Array.Empty<Permission>();

    // Navigation properties
    public virtual ICollection<User>? Users { get; set; }
    public virtual ICollection<UserRole>? UserRoles { get; set; }

    public Role() { }

    public static Role Create(
        Guid tenantId,
        string name,
        string? description,
        Permission[] permissions,
        Guid createdById)
    {
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = description,
            Permissions = permissions,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool HasPermission(Permission permission)
    {
        return Permissions.Contains(permission);
    }

    public void AddPermission(Permission permission)
    {
        if (!HasPermission(permission))
        {
            var newPermissions = new List<Permission>(Permissions) { permission };
            Permissions = newPermissions.ToArray();
        }
    }

    public void RemovePermission(Permission permission)
    {
        var newPermissions = Permissions.Where(p => p != permission).ToArray();
        Permissions = newPermissions;
    }
}
