using FluentAssertions;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using Xunit;

namespace KasahQMS.Tests.Unit.Domain.Entities;

public class QmsTaskTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _createdById = Guid.NewGuid();

    private QmsTask CreateTask(DateTime? dueDate = null)
    {
        return QmsTask.Create(_tenantId, "Test Task", "TSK-001", _createdById,
            "Task description", TaskPriority.Medium, dueDate);
    }

    [Fact]
    public void Create_ShouldSetDefaults()
    {
        var task = CreateTask();

        task.Id.Should().NotBeEmpty();
        task.TenantId.Should().Be(_tenantId);
        task.Title.Should().Be("Test Task");
        task.TaskNumber.Should().Be("TSK-001");
        task.Description.Should().Be("Task description");
        task.Priority.Should().Be(TaskPriority.Medium);
        task.Status.Should().Be(QmsTaskStatus.Open);
        task.CreatedById.Should().Be(_createdById);
        task.Tags.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Create_WithDueDate_ShouldSetDueDate()
    {
        var dueDate = DateTime.UtcNow.AddDays(7);
        var task = CreateTask(dueDate);

        task.DueDate.Should().NotBeNull();
    }

    [Fact]
    public void Assign_FromOpen_ShouldSetAssigneeAndMoveToInProgress()
    {
        var task = CreateTask();
        var userId = Guid.NewGuid();

        task.Assign(userId);

        task.AssignedToId.Should().Be(userId);
        task.Status.Should().Be(QmsTaskStatus.InProgress);
    }

    [Fact]
    public void Assign_FromRejected_ShouldMoveToInProgress()
    {
        var task = CreateTask();
        task.Status = QmsTaskStatus.Rejected;
        var userId = Guid.NewGuid();

        task.Assign(userId);

        task.AssignedToId.Should().Be(userId);
        task.Status.Should().Be(QmsTaskStatus.InProgress);
    }

    [Fact]
    public void Assign_FromInProgress_ShouldChangeAssigneeButKeepStatus()
    {
        var task = CreateTask();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        task.Assign(firstUser);

        task.Assign(secondUser);

        task.AssignedToId.Should().Be(secondUser);
        task.Status.Should().Be(QmsTaskStatus.InProgress);
    }

    [Fact]
    public void Complete_ShouldSetAwaitingApproval()
    {
        var task = CreateTask();
        var completedById = Guid.NewGuid();

        task.Complete(completedById, "Done");

        task.Status.Should().Be(QmsTaskStatus.AwaitingApproval);
        task.CompletedAt.Should().NotBeNull();
        task.CompletedById.Should().Be(completedById);
        task.CompletionNotes.Should().Be("Done");
    }

    [Fact]
    public void Approve_ShouldSetCompleted()
    {
        var task = CreateTask();
        var completedById = Guid.NewGuid();
        task.Complete(completedById);

        var approvedById = Guid.NewGuid();
        task.Approve(approvedById, "Looks good");

        task.Status.Should().Be(QmsTaskStatus.Completed);
        task.ApprovedById.Should().Be(approvedById);
        task.ApprovalRemarks.Should().Be("Looks good");
    }

    [Fact]
    public void Reject_ShouldSetRejectedWithRemarks()
    {
        var task = CreateTask();
        task.Complete(Guid.NewGuid());

        var rejectedById = Guid.NewGuid();
        task.Reject(rejectedById, "Not adequate");

        task.Status.Should().Be(QmsTaskStatus.Rejected);
        task.RejectionRemarks.Should().Be("Not adequate");
        task.RejectedById.Should().Be(rejectedById);
    }

    [Fact]
    public void Cancel_ShouldSetCancelledWithReason()
    {
        var task = CreateTask();

        task.Cancel("No longer needed");

        task.Status.Should().Be(QmsTaskStatus.Cancelled);
        task.CompletionNotes.Should().Be("No longer needed");
    }

    [Fact]
    public void Cancel_WithoutReason_ShouldSetCancelled()
    {
        var task = CreateTask();

        task.Cancel();

        task.Status.Should().Be(QmsTaskStatus.Cancelled);
    }

    [Fact]
    public void MarkOverdue_WhenPastDueAndOpen_ShouldSetOverdue()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(-1));

        task.MarkOverdue();

        task.Status.Should().Be(QmsTaskStatus.Overdue);
    }

    [Fact]
    public void MarkOverdue_WhenNotPastDue_ShouldNotChange()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(7));

        task.MarkOverdue();

        task.Status.Should().Be(QmsTaskStatus.Open);
    }

    [Fact]
    public void MarkOverdue_WhenCompleted_ShouldNotChange()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(-1));
        task.Status = QmsTaskStatus.Completed;

        task.MarkOverdue();

        task.Status.Should().Be(QmsTaskStatus.Completed);
    }

    [Fact]
    public void MarkOverdue_WhenCancelled_ShouldNotChange()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(-1));
        task.Status = QmsTaskStatus.Cancelled;

        task.MarkOverdue();

        task.Status.Should().Be(QmsTaskStatus.Cancelled);
    }

    [Fact]
    public void MarkOverdue_WhenAwaitingApproval_ShouldNotChange()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(-1));
        task.Status = QmsTaskStatus.AwaitingApproval;

        task.MarkOverdue();

        task.Status.Should().Be(QmsTaskStatus.AwaitingApproval);
    }

    [Fact]
    public void MarkOverdue_WhenNoDueDate_ShouldNotChange()
    {
        var task = CreateTask();

        task.MarkOverdue();

        task.Status.Should().Be(QmsTaskStatus.Open);
    }

    [Fact]
    public void AddTag_ShouldAddNewTag()
    {
        var task = CreateTask();

        task.AddTag("urgent");

        task.Tags.Should().Contain("urgent");
        task.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void AddTag_Duplicate_ShouldNotAddAgain()
    {
        var task = CreateTask();

        task.AddTag("urgent");
        task.AddTag("urgent");

        task.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void AddTag_MultipleDifferentTags_ShouldAddAll()
    {
        var task = CreateTask();

        task.AddTag("urgent");
        task.AddTag("review");
        task.AddTag("compliance");

        task.Tags.Should().HaveCount(3);
    }

    [Fact]
    public void LinkToDocument_ShouldSetLinkedDocumentId()
    {
        var task = CreateTask();
        var docId = Guid.NewGuid();

        task.LinkToDocument(docId);

        task.LinkedDocumentId.Should().Be(docId);
    }

    [Fact]
    public void LinkToCapa_ShouldSetLinkedCapaId()
    {
        var task = CreateTask();
        var capaId = Guid.NewGuid();

        task.LinkToCapa(capaId);

        task.LinkedCapaId.Should().Be(capaId);
    }

    [Fact]
    public void LinkToAudit_ShouldSetLinkedAuditId()
    {
        var task = CreateTask();
        var auditId = Guid.NewGuid();

        task.LinkToAudit(auditId);

        task.LinkedAuditId.Should().Be(auditId);
    }
}
