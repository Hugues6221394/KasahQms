using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Identity;

/// <summary>
/// Join entity between Role and Permission.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    
    public Permission Permission { get; set; }
    
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? GrantedBy { get; set; }
}

