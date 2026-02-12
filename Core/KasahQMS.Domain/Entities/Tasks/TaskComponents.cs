using KasahQMS.Domain.Enums;

namespace KasahQMS.Domain.Entities.Tasks;

/// <summary>
/// Comment on a task.
/// </summary>
public sealed class TaskComment
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public string Content { get; private set; } = null!;
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsEdited { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    
    private TaskComment() { }
    
    public static TaskComment Create(Guid taskId, string content, Guid createdBy)
    {
        return new TaskComment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Content = content.Trim(),
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
    
    public void Edit(string content)
    {
        Content = content.Trim();
        IsEdited = true;
        EditedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
/// File attachment on a task.
/// </summary>
public sealed class TaskAttachment
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public string FileName { get; private set; } = null!;
    public string StoragePath { get; private set; } = null!;
    public string? ContentType { get; private set; }
    public long? SizeBytes { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    
    private TaskAttachment() { }
    
    public static TaskAttachment Create(
        Guid taskId,
        string fileName,
        string storagePath,
        Guid uploadedBy,
        string? contentType = null,
        long? sizeBytes = null)
    {
        return new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            FileName = fileName.Trim(),
            StoragePath = storagePath,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedBy = uploadedBy,
            UploadedAt = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// History record for task state changes.
/// </summary>
public sealed class TaskHistory
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public string Action { get; private set; } = null!;
    public QmsTaskStatus Status { get; private set; }
    public Guid PerformedBy { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
    
    private TaskHistory() { }
    
    public static TaskHistory Create(
        Guid taskId,
        string action,
        QmsTaskStatus status,
        Guid performedBy)
    {
        return new TaskHistory
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            Action = action,
            Status = status,
            PerformedBy = performedBy,
            PerformedAt = DateTimeOffset.UtcNow
        };
    }
}

