using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Privacy;

/// <summary>
/// Entity recording a user's consent grant or revocation for a specific consent type.
/// </summary>
public class ConsentRecord : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public ConsentType ConsentType { get; set; }
    public bool IsGranted { get; set; }
    public DateTime? GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public ConsentRecord() { }
}
