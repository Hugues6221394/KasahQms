using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Domain.Entities.Tasks;

/// <summary>
/// Task activity entity for tracking work progress and updates.
/// </summary>
public class TaskActivity : BaseEntity
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? AttachmentPath { get; set; }
    public string? AttachmentName { get; set; }
    public int? ProgressPercentage { get; set; }
    
    // Navigation properties
    public virtual QmsTask? Task { get; set; }
    public virtual User? User { get; set; }
    
    public TaskActivity() { }
    
    public static TaskActivity Create(
        Guid taskId,
        Guid userId,
        string activityType,
        string description,
        int? progressPercentage = null,
        string? attachmentPath = null,
        string? attachmentName = null)
    {
        return new TaskActivity
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            UserId = userId,
            ActivityType = activityType,
            Description = description,
            ProgressPercentage = progressPercentage,
            AttachmentPath = attachmentPath,
            AttachmentName = attachmentName,
            CreatedAt = DateTime.UtcNow
        };
    }
}
