using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace KasahQMS.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Replaces PostgreSQL with InMemory database and configures test JWT authentication.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestSecretKey = "IntegrationTestSecretKeyThatIsAtLeast32CharsLong!";
    public const string TestIssuer = "KasahQMS";
    public const string TestAudience = "KasahQMS";
    public const string TestUserEmail = "sysadmin@kasah.com";
    public const string TestUserPassword = "P@ssw0rd!";

    // Seeded entity IDs populated after host creation
    public Guid TestTenantId { get; private set; }
    public Guid TestUserId { get; private set; }
    public Guid TestRoleId { get; private set; }

    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    // Static constructor ensures environment variables are set before any factory instance is created
    static TestWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", TestSecretKey);
        Environment.SetEnvironmentVariable("JwtSettings__Issuer", TestIssuer);
        Environment.SetEnvironmentVariable("JwtSettings__Audience", TestAudience);
        Environment.SetEnvironmentVariable("JwtSettings__AccessTokenExpirationMinutes", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();

            // Remove the DbContext registration added by AddPersistenceServices
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
                services.Remove(dbContextDescriptor);

            // Add InMemory database
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseInMemoryDatabase(_dbName);
            });
        });

        builder.ConfigureServices(services =>
        {
            // Disable HTTPS redirection by removing the middleware indirectly
            services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
            {
                options.HttpsPort = null;
            });
        });
    }

    /// <summary>
    /// Creates an HttpClient that has a valid JWT Bearer token for the test admin user.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Resolve IDs from the seeded database
        EnsureSeededIds();

        var token = GenerateTestJwtToken(TestUserId, TestTenantId, TestUserEmail, "System Admin", "System Admin");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Creates an unauthenticated HttpClient (no auth headers).
    /// </summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    /// <summary>
    /// Generates a JWT token for testing with the specified claims.
    /// </summary>
    public string GenerateTestJwtToken(
        Guid userId,
        Guid tenantId,
        string email,
        string fullName,
        params string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, fullName),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private void EnsureSeededIds()
    {
        if (TestTenantId != Guid.Empty)
            return;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Disable tenant filter to read all data
        db.DisableTenantFilter();

        var tenant = db.Tenants.FirstOrDefault(t => t.Code == "RW");
        if (tenant != null)
            TestTenantId = tenant.Id;

        var user = db.Users.FirstOrDefault(u => u.Email == TestUserEmail);
        if (user != null)
            TestUserId = user.Id;

        var role = db.Roles.FirstOrDefault(r => r.Name == "System Admin");
        if (role != null)
            TestRoleId = role.Id;
    }
}
