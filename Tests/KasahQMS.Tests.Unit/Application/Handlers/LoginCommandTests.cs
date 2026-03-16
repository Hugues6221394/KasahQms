using FluentAssertions;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Identity.Commands;
using KasahQMS.Application.Features.Identity.Dtos;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace KasahQMS.Tests.Unit.Application.Handlers;

public class LoginCommandTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<ICurrentUserService> _currentUserServiceMock;
    private readonly Mock<ILogger<LoginCommandHandler>> _loggerMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _auditLogServiceMock = new Mock<IAuditLogService>();
        _currentUserServiceMock = new Mock<ICurrentUserService>();
        _loggerMock = new Mock<ILogger<LoginCommandHandler>>();

        _currentUserServiceMock.Setup(x => x.IpAddress).Returns("127.0.0.1");
        _currentUserServiceMock.Setup(x => x.UserAgent).Returns("TestAgent");

        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _auditLogServiceMock.Object,
            _currentUserServiceMock.Object,
            _refreshTokenRepositoryMock.Object,
            _loggerMock.Object);
    }

    private static User CreateTestUser(bool isActive = true, bool isLockedOut = false, DateTime? lockoutEndTime = null)
    {
        var user = User.Create(
            Guid.NewGuid(),
            "test@example.com",
            "John",
            "Doe",
            "hashed_password",
            Guid.NewGuid());

        user.IsActive = isActive;
        user.IsLockedOut = isLockedOut;
        user.LockoutEndTime = lockoutEndTime;
        return user;
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ReturnsTokenAndUserInfo()
    {
        // Arrange
        var user = CreateTestUser();
        var command = new LoginCommand("test@example.com", "correct_password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify("correct_password", "hashed_password"))
            .Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync("access_token_123");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh_token_456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access_token_123");
        result.Value.RefreshToken.Should().Be("refresh_token_456");
        result.Value.UserId.Should().Be(user.Id);
        result.Value.Email.Should().Be("test@example.com");
        result.Value.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task Handle_WrongPassword_ReturnsFailure()
    {
        // Arrange
        var user = CreateTestUser();
        var command = new LoginCommand("test@example.com", "wrong_password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify("wrong_password", "hashed_password"))
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("not authorized");
    }

    [Fact]
    public async Task Handle_NonExistentUser_ReturnsFailure()
    {
        // Arrange
        var command = new LoginCommand("nonexistent@example.com", "password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("nonexistent@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_LockedOutUser_ReturnsFailure()
    {
        // Arrange
        var user = CreateTestUser(isLockedOut: true, lockoutEndTime: DateTime.UtcNow.AddMinutes(30));
        var command = new LoginCommand("test@example.com", "password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("forbidden");
    }

    [Fact]
    public async Task Handle_InactiveUser_ReturnsFailure()
    {
        // Arrange
        var user = CreateTestUser(isActive: false);
        var command = new LoginCommand("test@example.com", "password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorMessage.Should().Contain("not authorized");
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_RecordsLoginEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var command = new LoginCommand("test@example.com", "correct_password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify("correct_password", "hashed_password"))
            .Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync("token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync("refresh");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _auditLogServiceMock.Verify(x => x.LogAuthenticationAsync(
            user.Id,
            "LOGIN_SUCCESS",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        _userRepositoryMock.Verify(x => x.UpdateAsync(
            It.Is<User>(u => u.Id == user.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FailedLogin_IncrementsFailureCount()
    {
        // Arrange
        var user = CreateTestUser();
        var command = new LoginCommand("test@example.com", "wrong_password");

        _userRepositoryMock.Setup(x => x.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(x => x.Verify("wrong_password", "hashed_password"))
            .Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        user.FailedLoginAttempts.Should().Be(1);
        _userRepositoryMock.Verify(x => x.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _auditLogServiceMock.Verify(x => x.LogAuthenticationAsync(
            user.Id,
            "LOGIN_FAILED",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
