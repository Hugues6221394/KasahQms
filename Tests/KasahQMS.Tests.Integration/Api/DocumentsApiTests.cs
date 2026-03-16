using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KasahQMS.Application.Features.Documents.Dtos;

namespace KasahQMS.Tests.Integration.Api;

/// <summary>
/// Integration tests for the document CRUD API endpoints (GET/POST /api/documents/*).
/// </summary>
public class DocumentsApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DocumentsApiTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetDocuments_Authenticated_ReturnsPaginatedList()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/documents?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
        // The response should contain JSON with items, totalCount, etc.
        content.Should().Contain("items");
    }

    [Fact]
    public async Task CreateDocument_Authenticated_ReturnsCreatedWithId()
    {
        // Arrange
        var client = _factory.CreateAuthenticatedClient();
        var dto = new CreateDocumentDto
        {
            Title = "Integration Test Document",
            Description = "Created during integration testing",
            Content = "Test document content for integration tests"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/documents", dto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = await response.Content.ReadFromJsonAsync<Guid>();
        documentId.Should().NotBe(Guid.Empty);

        // Verify location header points to the created document
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDocumentById_AfterCreate_ReturnsDocumentDetails()
    {
        // Arrange - create a document first
        var client = _factory.CreateAuthenticatedClient();
        var dto = new CreateDocumentDto
        {
            Title = "Get By Id Test Document",
            Description = "Test doc for retrieval",
            Content = "Content for retrieval test"
        };

        var createResponse = await client.PostAsJsonAsync("/api/documents", dto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        var response = await client.GetAsync($"/api/documents/{documentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = await response.Content.ReadFromJsonAsync<DocumentDto>();
        document.Should().NotBeNull();
        document!.Title.Should().Be("Get By Id Test Document");
        document.Description.Should().Be("Test doc for retrieval");
        document.Id.Should().Be(documentId);
    }

    [Fact]
    public async Task SubmitDocument_DraftDocument_ReturnsOk()
    {
        // Arrange - create a document first (starts in Draft)
        var client = _factory.CreateAuthenticatedClient();
        var dto = new CreateDocumentDto
        {
            Title = "Submit Test Document",
            Description = "Test doc for submission",
            Content = "Content for submission test"
        };

        var createResponse = await client.PostAsJsonAsync("/api/documents", dto);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var documentId = await createResponse.Content.ReadFromJsonAsync<Guid>();

        // Act
        var response = await client.PostAsync($"/api/documents/{documentId}/submit", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("submitted");
    }

    [Fact]
    public async Task GetDocuments_Unauthenticated_ReturnsUnauthorizedOrRedirect()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/documents");

        // Assert - should be 401 (JWT) or 302 (cookie redirect)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Found);
    }

    [Fact]
    public async Task CreateDocument_Unauthenticated_ReturnsUnauthorizedOrRedirect()
    {
        // Arrange
        var client = _factory.CreateUnauthenticatedClient();
        var dto = new CreateDocumentDto
        {
            Title = "Unauthorized Doc",
            Description = "Should not be created"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/documents", dto);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Found);
    }
}
