using FluentAssertions;
using KasahQMS.Infrastructure.Services;

namespace KasahQMS.Tests.Unit.Infrastructure.Services;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_ReturnsNonNullNonEmptyString()
    {
        // Act
        var hash = _hasher.Hash("password123");

        // Assert
        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Hash_ReturnsDifferentResultsForSameInput()
    {
        // Act
        var hash1 = _hasher.Hash("password123");
        var hash2 = _hasher.Hash("password123");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        // Arrange
        var password = "MySecurePassword!";
        var hash = _hasher.Hash(password);

        // Act
        var result = _hasher.Verify(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        // Arrange
        var hash = _hasher.Hash("correct_password");

        // Act
        var result = _hasher.Verify("wrong_password", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_HandlesNullPassword_Gracefully()
    {
        // Arrange
        var hash = _hasher.Hash("password");

        // Act
        Action act = () => _hasher.Verify(null!, hash);

        // Assert
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Verify_HandlesEmptyHash_Gracefully()
    {
        // Act
        Action act = () => _hasher.Verify("password", "");

        // Assert
        act.Should().Throw<Exception>();
    }
}
