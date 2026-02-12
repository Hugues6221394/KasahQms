using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Notifications;

/// <summary>
/// Notification type enum.
/// </summary>
public enum NotificationType
{
    System,
    TaskAssignment,
    TaskOverdue,
    TaskUpdate,
    DocumentSubmitted,
    DocumentApproval,
    DocumentRejection,
    AuditScheduled,
    AuditFinding,
    CapaAssignment,
    CapaOverdue,
    CapaVerification
}

/// <summary>
/// Notification entity for user notifications.
/// </summary>
public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // Navigation properties
    public virtual User? User { get; set; }
    
    public Notification() { }
    
    public static Notification Create(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        Guid? relatedEntityId = null,
        string? relatedEntityType = null)
    {
        return new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
    }
    
    public void Dismiss()
    {
        IsRead = true;
        ReadAt ??= DateTime.UtcNow;
    }
}
