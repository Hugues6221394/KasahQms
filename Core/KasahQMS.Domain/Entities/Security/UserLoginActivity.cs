using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Security;

/// <summary>
/// Entity for tracking user login/logout activities for supervision.
/// </summary>
public class UserLoginActivity : AuditableEntity
{
    public Guid UserId { get; set; }
    public string EventType { get; set; } = string.Empty; // Login, Logout, SessionExpired
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    
    // Navigation
    public virtual User? User { get; set; }
    
    public UserLoginActivity() { }
    
    public static UserLoginActivity Create(
        Guid tenantId,
        Guid userId,
        string eventType,
        string? ipAddress = null,
        string? userAgent = null,
        string? deviceInfo = null)
    {
        return new UserLoginActivity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DeviceInfo = deviceInfo,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow
        };
    }
}
