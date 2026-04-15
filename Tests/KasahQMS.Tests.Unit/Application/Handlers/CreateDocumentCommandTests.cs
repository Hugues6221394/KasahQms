using FluentAssertions;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Documents.Commands;
using KasahQMS.Domain.Entities.Documents;
using Microsoft.Extensions.Logging;
using Moq;

namespace KasahQMS.Tests.Unit.Application.Handlers;

public class CreateDocumentCommandTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateDocumentCommandHandler>> _loggerMock;
    private readonly CreateDocumentCommandHandler _handler;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public CreateDocumentCommandTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _emailServiceMock = new Mock<IEmailService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _auditLogServiceMock = new Mock<IAuditLogService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateDocumentCommandHandler>>();

        _currentUserServiceMock.Setup(x => x.UserId).Returns(_userId);
        _currentUserServiceMock.Setup(x => x.TenantId).Returns(_tenantId);

        _handler = new CreateDocumentCommandHandler(
            _documentRepositoryMock.Object,
            _userRepositoryMock.Object,
            _notificationServiceMock.Object,
            _emailServiceMock.Object,
            _currentUserServiceMock.Object,
            _auditLogServiceMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_CreatesDocumentWithCorrectProperties()
    {
        // Arrange
        var typeId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var command = new CreateDocumentCommand(
            Title: "Test Document",
            Description: "A test description",
            Content: null,
            DocumentTypeId: typeId,
            CategoryId: categoryId);

        _documentRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        Document? capturedDocument = null;
        _documentRepositoryMock.Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => capturedDocument = doc)
            .ReturnsAsync((Document doc, CancellationToken _) => doc);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        capturedDocument.Should().NotBeNull();
        capturedDocument!.Title.Should().Be("Test Document");
        capturedDocument.Description.Should().Be("A test description");
        capturedDocument.DocumentTypeId.Should().Be(typeId);
        capturedDocument.CategoryId.Should().Be(categoryId);
        capturedDocument.TenantId.Should().Be(_tenantId);
        capturedDocument.CreatedById.Should().Be(_userId);
    }

    [Fact]
    public async Task Handle_GeneratesDocumentNumber()
    {
        // Arrange
        var command = new CreateDocumentCommand(
            Title: "Test",
            Description: null,
            Content: null,
            DocumentTypeId: null,
            CategoryId: null);

        _documentRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, DateTime.UtcNow.Year, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        Document? capturedDocument = null;
        _documentRepositoryMock.Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Callback<Document, CancellationToken>((doc, _) => capturedDocument = doc)
            .ReturnsAsync((Document doc, CancellationToken _) => doc);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        capturedDocument.Should().NotBeNull();
        capturedDocument!.DocumentNumber.Should().Be($"DOC-{DateTime.UtcNow.Year}-00043");
    }

    [Fact]
    public async Task Handle_ReturnsSuccessWithDocumentId()
    {
        // Arrange
        var command = new CreateDocumentCommand(
            Title: "Test",
            Description: null,
            Content: null,
            DocumentTypeId: null,
            CategoryId: null);

        _documentRepositoryMock.Setup(x => x.GetCountForYearAsync(_tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _documentRepositoryMock.Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document doc, CancellationToken _) => doc);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullUserId_ReturnsUnauthorized()
    {
        // Arrange
        _currentUserServiceMock.Setup(x => x.UserId).Returns((Guid?)null);
        var command = new CreateDocumentCommand(
            Title: "Test",
            Description: null,
            Content: null,
            DocumentTypeId: null,
            CategoryId: null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
