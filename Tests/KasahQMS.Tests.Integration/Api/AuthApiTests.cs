using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KasahQMS.Application.Features.Identity.Dtos;

namespace KasahQMS.Tests.Integration.Api;

/// <summary>
/// Integration tests for the authentication API endpoints (POST /api/auth/*).
/// </summary>
public class AuthApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithToken()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var request = new LoginRequestDto
        {
            Email = TestWebApplicationFactory.TestUserEmail,
            Password = TestWebApplicationFactory.TestUserPassword
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be(TestWebApplicationFactory.TestUserEmail);
        result.UserId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var request = new LoginRequestDto
        {
            Email = TestWebApplicationFactory.TestUserEmail,
            Password = "WrongPassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var request = new LoginRequestDto
        {
            Email = "nonexistent@kasah.com",
            Password = "AnyPassword123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokens()
    {
        // Arrange - first login to get a valid refresh token
        var client = _factory.CreateUnauthenticatedClient();
        var loginRequest = new LoginRequestDto
        {
            Email = TestWebApplicationFactory.TestUserEmail,
            Password = TestWebApplicationFactory.TestUserPassword
        };

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        loginResult.Should().NotBeNull();

        // Act - use the refresh token
        var refreshRequest = new RefreshTokenRequestDto
        {
            RefreshToken = loginResult!.RefreshToken
        };
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        refreshResult.Should().NotBeNull();
        refreshResult!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResult.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Logout_WithAuthenticatedUser_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutAuthentication_ReturnsUnauthorizedOrRedirect()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act - try to access a protected endpoint without authentication
        var response = await client.GetAsync("/api/documents");

        // Assert - should be 401 (JWT) or 302 (cookie redirect)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Found);
    }
}
