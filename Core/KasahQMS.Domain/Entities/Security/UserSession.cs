using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Security;

/// <summary>
/// Entity representing an active user session with device and location tracking.
/// </summary>
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// Hash of the refresh token associated with this session.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public UserSession() { }

    /// <summary>
    /// Whether the session has expired based on the current time.
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Revokes this session.
    /// </summary>
    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the last activity timestamp.
    /// </summary>
    public void UpdateActivity()
    {
        LastActivityAt = DateTime.UtcNow;
    }
}
