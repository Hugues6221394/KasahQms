using FluentAssertions;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Tasks.Commands;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace KasahQMS.Tests.Unit.Application.Handlers;

public class CreateTaskCommandTests
{
    private readonly Mock<ITaskRepository> _taskRepositoryMock;
    private readonly Mock<ITaskAssignmentRepository> _taskAssignmentRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateTaskCommandHandler>> _loggerMock;
    private readonly CreateTaskCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateTaskCommandTests()
    {
        _taskRepositoryMock = new Mock<ITaskRepository>();
        _taskAssignmentRepositoryMock = new Mock<ITaskAssignmentRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _auditLogServiceMock = new Mock<IAuditLogService>();
        _notificationServiceMock = new Mock<INotificationService>();
        _emailServiceMock = new Mock<IEmailService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateTaskCommandHandler>>();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(_userId);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(_tenantId);

        _handler = new CreateTaskCommandHandler(
            _taskRepositoryMock.Object,
            _taskAssignmentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _currentUserServiceMock.Object,
            _auditLogServiceMock.Object,
            _notificationServiceMock.Object,
            _emailServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_CreatesTaskWithCorrectProperties()
    {
        // Arrange
        var dueDate = DateTime.UtcNow.AddDays(7);
        var command = new CreateTaskCommand(
            Title: "Review Budget",
            Description: "Review Q4 budget",
            AssignedToId: null,
            DueDate: dueDate,
            Priority: TaskPriority.High,
            LinkedDocumentId: null,
            LinkedCapaId: null,
            LinkedAuditId: null);

        _taskRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        QmsTask? capturedTask = null;
        _taskRepositoryMock.Setup(x => x.AddAsync(It.IsAny<QmsTask>(), It.IsAny<CancellationToken>()))
            .Callback<QmsTask, CancellationToken>((task, _) => capturedTask = task)
            .ReturnsAsync((QmsTask task, CancellationToken _) => task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedTask.Should().NotBeNull();
        capturedTask!.Title.Should().Be("Review Budget");
        capturedTask.Description.Should().Be("Review Q4 budget");
        capturedTask.Priority.Should().Be(TaskPriority.High);
        capturedTask.TenantId.Should().Be(_tenantId);
        capturedTask.CreatedById.Should().Be(_userId);
        capturedTask.TaskNumber.Should().Be($"TASK-{DateTime.UtcNow.Year}-00011");
    }

    [Fact]
    public async Task Handle_SetsLinkedDocumentAndCapa()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var capaId = Guid.NewGuid();
        var command = new CreateTaskCommand(
            Title: "Linked Task",
            Description: null,
            AssignedToId: null,
            DueDate: null,
            Priority: TaskPriority.Medium,
            LinkedDocumentId: docId,
            LinkedCapaId: capaId,
            LinkedAuditId: null);

        _taskRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        QmsTask? capturedTask = null;
        _taskRepositoryMock.Setup(x => x.AddAsync(It.IsAny<QmsTask>(), It.IsAny<CancellationToken>()))
            .Callback<QmsTask, CancellationToken>((task, _) => capturedTask = task)
            .ReturnsAsync((QmsTask task, CancellationToken _) => task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedTask.Should().NotBeNull();
        capturedTask!.LinkedDocumentId.Should().Be(docId);
        capturedTask.LinkedCapaId.Should().Be(capaId);
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithTaskId()
    {
        // Arrange
        var command = new CreateTaskCommand(
            Title: "Simple Task",
            Description: null,
            AssignedToId: null,
            DueDate: null,
            Priority: TaskPriority.Low,
            LinkedDocumentId: null,
            LinkedCapaId: null,
            LinkedAuditId: null);

        _taskRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _taskRepositoryMock.Setup(x => x.AddAsync(It.IsAny<QmsTask>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QmsTask task, CancellationToken _) => task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditLogServiceMock.Verify(x => x.LogAsync(
            "TASK_CREATED",
            "Tasks",
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullUserId_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserServiceMock.Setup(x => x.UserId).Returns((Guid?)null);
        var command = new CreateTaskCommand(
            Title: "Task",
            Description: null,
            AssignedToId: null,
            DueDate: null,
            Priority: TaskPriority.Medium,
            LinkedDocumentId: null,
            LinkedCapaId: null,
            LinkedAuditId: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithAssignee_AssignsTask()
    {
        // Arrange
        var assigneeId = Guid.NewGuid();
        var command = new CreateTaskCommand(
            Title: "Assigned Task",
            Description: null,
            AssignedToId: assigneeId,
            DueDate: null,
            Priority: TaskPriority.Medium,
            LinkedDocumentId: null,
            LinkedCapaId: null,
            LinkedAuditId: null);

        _taskRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        QmsTask? capturedTask = null;
        _taskRepositoryMock.Setup(x => x.AddAsync(It.IsAny<QmsTask>(), It.IsAny<CancellationToken>()))
            .Callback<QmsTask, CancellationToken>((task, _) => capturedTask = task)
            .ReturnsAsync((QmsTask task, CancellationToken _) => task);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedTask.Should().NotBeNull();
        capturedTask!.AssignedToId.Should().Be(assigneeId);
    }
}
