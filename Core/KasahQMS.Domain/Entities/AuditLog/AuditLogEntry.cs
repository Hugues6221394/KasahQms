using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.AuditLog;

/// <summary>
/// Audit log entry for tracking all system actions.
/// </summary>
public class AuditLogEntry : BaseEntity
{
    public Guid? TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Description { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsSuccessful { get; set; } = true;
    public string? FailureReason { get; set; }
    
    // Navigation properties
    public virtual User? User { get; set; }
    
    public AuditLogEntry() { }
    
    public static AuditLogEntry Create(
        string action,
        string entityType,
        Guid? entityId = null,
        string? description = null,
        Guid? userId = null,
        Guid? tenantId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? oldValues = null,
        string? newValues = null)
    {
        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            IsSuccessful = true
        };
    }
    
    public static AuditLogEntry CreateAuthenticationLog(
        Guid? userId,
        string action,
        string? description = null,
        string? ipAddress = null,
        string? userAgent = null,
        bool isSuccessful = true,
        string? failureReason = null)
    {
        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityType = "Authentication",
            Description = description,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = DateTime.UtcNow,
            IsSuccessful = isSuccessful,
            FailureReason = failureReason
        };
    }
}
