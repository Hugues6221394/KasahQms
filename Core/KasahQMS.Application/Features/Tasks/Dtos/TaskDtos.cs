using KasahQMS.Domain.Enums;

namespace KasahQMS.Application.Features.Tasks.Dtos;

public class TaskDto
{
    public Guid Id { get; set; }
    public string TaskNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public QmsTaskStatus Status { get; set; }
    public TaskPriority Priority { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? AssignedToName { get; set; }
    public Guid? LinkedDocumentId { get; set; }
    public Guid? LinkedCapaId { get; set; }
    public Guid? LinkedAuditId { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class CreateTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public Guid? LinkedDocumentId { get; set; }
    public Guid? LinkedCapaId { get; set; }
    public Guid? LinkedAuditId { get; set; }
}

public class UpdateTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssignedToId { get; set; }
    public DateTime? DueDate { get; set; }
    public TaskPriority Priority { get; set; }
}
