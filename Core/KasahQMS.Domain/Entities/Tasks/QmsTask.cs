using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Tasks;

/// <summary>
/// Task entity for tracking work items.
/// </summary>
public class QmsTask : AuditableEntity
{
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public QmsTaskStatus Status { get; set; } = QmsTaskStatus.Open;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletionNotes { get; set; }
    public string? ReviewerRemarks { get; set; }
    public Guid? LinkedDocumentId { get; set; }
    public Guid? LinkedCapaId { get; set; }
    public Guid? LinkedAuditId { get; set; }
    /// <summary>When set, task is assigned to this department (TMD/Deputy).</summary>
    public Guid? AssignedToOrgUnitId { get; set; }
    public List<string> Tags { get; set; } = new();
    
    // Navigation properties
    public virtual User? AssignedTo { get; set; }
    public virtual User? CompletedBy { get; set; }
    public virtual Document? LinkedDocument { get; set; }
    
    public QmsTask() { }
    
    public static QmsTask Create(
        Guid tenantId,
        string title,
        string taskNumber,
        Guid createdById,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null)
    {
        // Ensure DueDate is in UTC for PostgreSQL compatibility
        DateTime? utcDueDate = null;
        if (dueDate.HasValue)
        {
            utcDueDate = dueDate.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dueDate.Value, DateTimeKind.Utc)
                : dueDate.Value.ToUniversalTime();
        }

        return new QmsTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskNumber = taskNumber,
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = utcDueDate,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
            Status = QmsTaskStatus.Open,
            Tags = new List<string>()
        };
    }
    
    public void SetDescription(string description) => Description = description;
    
    public void Assign(Guid userId)
    {
        AssignedToId = userId;
        if (Status == QmsTaskStatus.Open || Status == QmsTaskStatus.Rejected)
        {
            Status = QmsTaskStatus.InProgress;
        }
    }
    
    public void LinkToDocument(Guid documentId) => LinkedDocumentId = documentId;
    public void LinkToCapa(Guid capaId) => LinkedCapaId = capaId;
    public void LinkToAudit(Guid auditId) => LinkedAuditId = auditId;
    
    public void AddTag(string tag)
    {
        Tags ??= new List<string>();
        if (!Tags.Contains(tag))
        {
            Tags.Add(tag);
        }
    }
    
    public void Complete(Guid completedById, string? notes = null)
    {
        Status = QmsTaskStatus.AwaitingApproval;
        CompletedAt = DateTime.UtcNow;
        CompletedById = completedById;
        CompletionNotes = notes;
    }

    public void Approve()
    {
        Status = QmsTaskStatus.Completed;
    }

    public void Reject(string remarks)
    {
        Status = QmsTaskStatus.Rejected;
        ReviewerRemarks = remarks;
    }
    
    public void Cancel(string? reason = null)
    {
        Status = QmsTaskStatus.Cancelled;
        CompletionNotes = reason;
    }
    
    public void MarkOverdue()
    {
        if (Status != QmsTaskStatus.Completed && 
            Status != QmsTaskStatus.Cancelled && 
            Status != QmsTaskStatus.AwaitingApproval &&
            DueDate.HasValue && 
            DueDate < DateTime.UtcNow)
        {
            Status = QmsTaskStatus.Overdue;
        }
    }
}
