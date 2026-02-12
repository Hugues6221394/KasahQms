using System.Text;
using KasahQMS.Application;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure;
using KasahQMS.Infrastructure.BackgroundJobs;
using KasahQMS.Infrastructure.Persistence;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace KasahQMS.Web.Extensions;

/// <summary>
/// Extension methods for configuring services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all application services and configurations.
    /// </summary>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add layer-specific services
        services.AddApplicationLayer();
        services.AddInfrastructureServices(configuration);
        services.AddPersistenceServices(configuration);

        // Add presentation layer services
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        return services;
    }

    /// <summary>
    /// Adds JWT authentication configuration.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt");
        var key = Encoding.UTF8.GetBytes(jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured"));

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = true;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidateAudience = true,
                ValidAudience = jwtSettings["Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            // Handle token in cookies for server-rendered pages
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Check for token in cookie
                    if (context.Request.Cookies.ContainsKey("auth_token"))
                    {
                        context.Token = context.Request.Cookies["auth_token"];
                    }
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    // Log authentication failures
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<JwtBearerEvents>>();
                    logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            // Add default policy
            options.AddPolicy("Authenticated", policy =>
                policy.RequireAuthenticatedUser());

            // Add role-based policies
            options.AddPolicy("SystemAdmin", policy =>
                policy.RequireRole("SystemAdmin"));

            options.AddPolicy("TenantAdmin", policy =>
                policy.RequireRole("TenantAdmin", "SystemAdmin"));

            options.AddPolicy("Manager", policy =>
                policy.RequireRole("TopManagingDirector", "DeputyDirector", "DepartmentManager", "TenantAdmin", "SystemAdmin"));

            options.AddPolicy("Auditor", policy =>
                policy.RequireRole("Auditor", "TenantAdmin", "SystemAdmin"));
        });

        return services;
    }

    /// <summary>
    /// Adds background job services.
    /// </summary>
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddHostedService<TaskOverdueCheckJob>();
        services.AddHostedService<CapaDeadlineCheckJob>();
        services.AddHostedService<DataMaintenanceJob>();

        return services;
    }

    /// <summary>
    /// Adds health check services.
    /// </summary>
    public static IServiceCollection AddHealthCheckServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), 
                tags: new[] { "live" });

        return services;
    }

    /// <summary>
    /// Adds CORS configuration.
    /// </summary>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddPolicy("Default", builder =>
            {
                if (allowedOrigins.Length > 0)
                {
                    builder.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                }
                else
                {
                    // Development: allow all
                    builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Adds response caching and compression.
    /// </summary>
    public static IServiceCollection AddCachingAndCompression(this IServiceCollection services)
    {
        services.AddResponseCaching();
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        return services;
    }
}

