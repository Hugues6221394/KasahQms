using KasahQMS.Domain.Common;

namespace KasahQMS.Domain.Entities.AuditLog;

/// <summary>
/// Per-user visibility state for audit log entries shown in approval history.
/// </summary>
public class UserAuditLogHistoryState : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid AuditLogEntryId { get; set; }
    public bool IsArchived { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

