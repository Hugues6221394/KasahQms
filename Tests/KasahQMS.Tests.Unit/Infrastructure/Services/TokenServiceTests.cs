using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace KasahQMS.Tests.Unit.Infrastructure.Services;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly User _testUser;

    public TokenServiceTests()
    {
        var jwtSection = new Mock<IConfigurationSection>();
        jwtSection.Setup(x => x[It.Is<string>(k => k == "SecretKey")])
            .Returns("ThisIsAVeryLongSecretKeyForTestingPurposesOnly1234567890!");
        jwtSection.Setup(x => x[It.Is<string>(k => k == "Issuer")])
            .Returns("KasahQMS-Test");
        jwtSection.Setup(x => x[It.Is<string>(k => k == "Audience")])
            .Returns("KasahQMS-Test");
        jwtSection.Setup(x => x[It.Is<string>(k => k == "AccessTokenExpirationMinutes")])
            .Returns("60");

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSection.Object);

        _tokenService = new TokenService(configMock.Object);

        _testUser = User.Create(
            Guid.NewGuid(),
            "test@example.com",
            "John",
            "Doe",
            "hashed_password",
            Guid.NewGuid());
    }

    [Fact]
    public async Task GenerateAccessToken_ReturnsNonEmptyString()
    {
        // Act
        var token = await _tokenService.GenerateAccessToken(_testUser);

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateAccessToken_ContainsExpectedClaims()
    {
        // Act
        var token = await _tokenService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == _testUser.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id" && c.Value == _testUser.TenantId.ToString());
    }

    [Fact]
    public async Task GenerateAccessToken_WithRoles_ContainsRoleClaims()
    {
        // Arrange - create a user with roles by using reflection since Roles is a navigation property
        var role = new Role { Id = Guid.NewGuid(), Name = "Admin" };
        _testUser.Roles = new List<Role> { role };

        // Act
        var token = await _tokenService.GenerateAccessToken(_testUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public async Task GenerateRefreshToken_ReturnsNonEmptyString()
    {
        // Act
        var token = await _tokenService.GenerateRefreshToken(Guid.NewGuid());

        // Assert
        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateRefreshToken_ReturnsUniqueValues()
    {
        // Act
        var userId = Guid.NewGuid();
        var token1 = await _tokenService.GenerateRefreshToken(userId);
        var token2 = await _tokenService.GenerateRefreshToken(userId);

        // Assert
        token1.Should().NotBe(token2);
    }
}
