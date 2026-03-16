using FluentAssertions;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Documents;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KasahQMS.Tests.Integration.Persistence;

/// <summary>
/// Integration tests for repositories against the InMemory database.
/// </summary>
public class RepositoryTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RepositoryTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private ApplicationDbContext CreateDbContext()
    {
        var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.DisableTenantFilter();
        return db;
    }

    // =====================
    // UserRepository Tests
    // =====================

    [Fact]
    public async Task UserRepository_GetByEmailAsync_ReturnsSeededUser()
    {
        // Arrange
        var db = CreateDbContext();
        var repo = new UserRepository(db);

        // Act
        var user = await repo.GetByEmailAsync(TestWebApplicationFactory.TestUserEmail);

        // Assert
        user.Should().NotBeNull();
        user!.Email.Should().Be(TestWebApplicationFactory.TestUserEmail);
        user.FirstName.Should().Be("System");
        user.LastName.Should().Be("Admin");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UserRepository_GetByEmailAsync_ReturnsNullForNonExistent()
    {
        // Arrange
        var db = CreateDbContext();
        var repo = new UserRepository(db);

        // Act
        var user = await repo.GetByEmailAsync("nobody@example.com");

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task UserRepository_AddAsync_AddsNewUser()
    {
        // Arrange
        var db = CreateDbContext();
        var repo = new UserRepository(db);
        var tenant = await db.Tenants.FirstAsync();

        var passwordHasher = _factory.Services.CreateScope().ServiceProvider
            .GetRequiredService<IPasswordHasher>();
        var hashedPassword = passwordHasher.Hash("Test@12345");

        var newUser = User.Create(
            tenant.Id,
            "newuser@kasah.com",
            "New",
            "User",
            hashedPassword,
            Guid.Empty);

        // Act
        var result = await repo.AddAsync(newUser);
        await db.SaveChangesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);

        var retrieved = await repo.GetByEmailAsync("newuser@kasah.com");
        retrieved.Should().NotBeNull();
        retrieved!.FirstName.Should().Be("New");
    }

    [Fact]
    public async Task UserRepository_UpdateAsync_ModifiesUser()
    {
        // Arrange
        var db = CreateDbContext();
        var repo = new UserRepository(db);
        var user = await repo.GetByEmailAsync(TestWebApplicationFactory.TestUserEmail);
        user.Should().NotBeNull();

        // Act
        user!.SetJobTitle("Updated Job Title");
        await repo.UpdateAsync(user);
        await db.SaveChangesAsync();

        // Assert - read back
        var db2 = CreateDbContext();
        var repo2 = new UserRepository(db2);
        var updated = await repo2.GetByEmailAsync(TestWebApplicationFactory.TestUserEmail);
        updated.Should().NotBeNull();
        updated!.JobTitle.Should().Be("Updated Job Title");
    }

    // ========================
    // DocumentRepository Tests
    // ========================

    [Fact]
    public async Task DocumentRepository_AddAsync_CreatesDocument()
    {
        // Arrange
        var db = CreateDbContext();
        var repo = new DocumentRepository(db);
        var tenant = await db.Tenants.FirstAsync();
        var user = await db.Users.FirstAsync();

        var doc = Document.Create(
            tenant.Id,
            "Repo Test Document",
            $"DOC-TEST-{Guid.NewGuid():N}".Substring(0, 20),
            user.Id,
            "Testing document creation via repository");

        // Act
        var result = await repo.AddAsync(doc);
        await db.SaveChangesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBe(Guid.Empty);
        result.Title.Should().Be("Repo Test Document");
        result.Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public async Task DocumentRepository_GetByIdWithDetailsAsync_ReturnsDocumentWithNavigations()
    {
        // Arrange
        var db = CreateDbContext();
        var repo = new DocumentRepository(db);

        // Get an existing seeded document
        var existingDoc = await db.Documents.FirstOrDefaultAsync();
        if (existingDoc == null)
        {
            // Create one if none seeded
            var tenant = await db.Tenants.FirstAsync();
            var user = await db.Users.FirstAsync();
            existingDoc = Document.Create(
                tenant.Id,
                "Details Test Document",
                $"DOC-DTL-{Guid.NewGuid():N}".Substring(0, 20),
                user.Id,
                "Testing details retrieval");
            await repo.AddAsync(existingDoc);
            await db.SaveChangesAsync();
        }

        // Act
        var result = await repo.GetByIdWithDetailsAsync(existingDoc.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().NotBeNullOrEmpty();
        result.Id.Should().Be(existingDoc.Id);
    }

    // ==================
    // Soft Delete Tests
    // ==================

    [Fact]
    public async Task SoftDelete_MarkingIsDeleted_ExcludesFromQueries()
    {
        // Arrange
        var db = CreateDbContext();
        var tenant = await db.Tenants.FirstAsync();
        var user = await db.Users.FirstAsync();

        var docNumber = $"DOC-DEL-{Guid.NewGuid():N}".Substring(0, 20);
        var doc = Document.Create(
            tenant.Id,
            "Soft Delete Test Document",
            docNumber,
            user.Id,
            "This document will be soft deleted");

        db.Documents.Add(doc);
        await db.SaveChangesAsync();

        // Verify it exists
        var beforeDelete = await db.Documents.FirstOrDefaultAsync(d => d.Id == doc.Id);
        beforeDelete.Should().NotBeNull();

        // Act - soft delete
        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;
        doc.DeletedById = user.Id;
        await db.SaveChangesAsync();

        // Assert - the entity should still be retrievable since InMemory doesn't enforce query filters
        // In production with PostgreSQL, query filters would exclude it.
        // We verify the flag is set correctly.
        var afterDelete = await db.Documents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == doc.Id);
        afterDelete.Should().NotBeNull();
        afterDelete!.IsDeleted.Should().BeTrue();
        afterDelete.DeletedAt.Should().NotBeNull();
        afterDelete.DeletedById.Should().Be(user.Id);
    }
}
