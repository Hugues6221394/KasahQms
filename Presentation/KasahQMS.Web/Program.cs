using System.Text;
using KasahQMS.Application;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure;
using KasahQMS.Infrastructure.Persistence;
using KasahQMS.Infrastructure.BackgroundJobs;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Web.Hubs;
using KasahQMS.Web.Middleware;


var builder = WebApplication.CreateBuilder(args);

// ===========================================
// Service Registration
// ===========================================

// Persist Data Protection keys so cookies survive app restarts
builder.Services.AddDataProtection();

// In-memory cache for badge tracking
builder.Services.AddMemoryCache();

// Add services from Clean Architecture layers
builder.Services.AddApplicationLayer();
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);

// HTTP Context for current user service
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<DashboardRoutingService>();

// ===========================================
// Authorization & Security Services (NEW)
// ===========================================



// Authorization service for permission checking
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

// Document state machine for workflow enforcement
builder.Services.AddScoped<IDocumentStateService, DocumentStateService>();

// Audit logging service
builder.Services.AddScoped<IAuditLoggingService, AuditLoggingService>();

// Workflow routing for approval workflows
builder.Services.AddScoped<IWorkflowRoutingService, WorkflowRoutingService>();

// Real-time notification service for badges and toasts
builder.Services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();

// Stock management service
builder.Services.AddScoped<IStockService, StockService>();

// SignalR
builder.Services.AddSignalR();
builder.Services.AddScoped<IPushNotificationSender, SignalRPushNotificationSender>();
builder.Services.AddHostedService<TaskOverdueCheckJob>();

// ===========================================
// Authentication Configuration
// ===========================================

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

// Security: Validate JWT secret key exists, meets minimum length, and is not a placeholder
if (string.IsNullOrEmpty(secretKey) || secretKey.Length < 32)
{
    throw new InvalidOperationException(
        "JWT SecretKey must be configured and be at least 32 characters long. " +
        "Set it via environment variable 'JwtSettings__SecretKey' or in appsettings.");
}
if (secretKey.Contains("YourSuperSecretKey", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "JWT SecretKey contains a placeholder value. Configure a real secret for this environment.");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "SmartScheme";
    options.DefaultChallengeScheme = "SmartScheme";
})
.AddPolicyScheme("SmartScheme", "JWT or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "KasahQmsAuth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "KasahQMS",
        ValidAudience = jwtSettings["Audience"] ?? "KasahQMS",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero // Strict token expiration
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

// ===========================================
// MVC and Razor Pages
// ===========================================

builder.Services.AddControllersWithViews(options =>
{
    // Security: Global authorization filter — all MVC actions require auth by default
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
});

// Swagger/OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "KASAH QMS API",
        Version = "v1",
        Description = "Kasah Quality Management System API"
    });
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Privacy/Index");
    options.Conventions.AllowAnonymousToPage("/Terms/Index");
    options.Conventions.AllowAnonymousToPage("/Support/Index");
});

// ===========================================
// CORS Configuration (Restrictive)
// ===========================================

builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "https://localhost:5001" };

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });

    // Development policy - still restricted but allows localhost
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins(
                "https://localhost:5001",
                "https://localhost:7001",
                "http://localhost:5000",
                "http://localhost:3000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ===========================================
// Rate Limiting
// ===========================================

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global default rate limit for all endpoints
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    // General API rate limit
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    // Strict limit for authentication endpoints (brute-force protection)
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });

    // Sliding window for document uploads
    options.AddSlidingWindowLimiter("upload", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(5);
        limiterOptions.SegmentsPerWindow = 5;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });
});

// ===========================================
// Health Checks
// ===========================================

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("Database");
// Note: For NpgSQL-specific health checks, install: dotnet add package AspNetCore.HealthChecks.NpgSql
// Then add: .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "", name: "PostgreSQL")

// ===========================================
// Build Application
// ===========================================

var app = builder.Build();

// ===========================================
// Database Initialization & Seeding
// ===========================================

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Skip relational migrations for InMemory provider (used in integration tests)
        if (dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migrations...", pendingMigrations.Count());
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("Database is up to date.");
            }
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync();
            logger.LogInformation("In-memory database created.");
        }

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
        throw;
    }
}

// ===========================================
// Middleware Pipeline
// ===========================================

// Error Handling — structured JSON for API; friendly pages for browsers
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // Enable Swagger UI in development
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "KASAH QMS API v1");
        options.RoutePrefix = "api/docs";
        options.DefaultModelsExpandDepth(2);
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Security: Force HTTPS
app.UseHttpsRedirection();

// Security: Comprehensive Security Headers
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    // Prevent clickjacking
    headers["X-Frame-Options"] = "DENY";

    // Prevent MIME type sniffing
    headers["X-Content-Type-Options"] = "nosniff";

    // XSS Protection (legacy but still useful)
    headers["X-XSS-Protection"] = "1; mode=block";

    // Referrer Policy
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // Permissions Policy (formerly Feature Policy)
    headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()";

    // Content Security Policy (allow SignalR WebSocket connections)
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' wss: ws:; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self';";

    // Strict Transport Security (HSTS)
    if (!context.Request.Host.Host.Contains("localhost"))
    {
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
    }

    // Cache control for sensitive pages
    if (context.Request.Path.StartsWithSegments("/Account") ||
        context.Request.Path.StartsWithSegments("/api"))
    {
        headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
        headers["Pragma"] = "no-cache";
        headers["Expires"] = "0";
    }

    await next();
});

// Static Files with caching
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 1 year in production
        if (!app.Environment.IsDevelopment())
        {
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        }
    }
});

// Routing
app.UseRouting();

// Rate Limiting
app.UseRateLimiter();

// Rate Limiting Response Headers
app.Use(async (context, next) =>
{
    await next();

    if (context.Response.Headers.ContainsKey("Retry-After"))
    {
        context.Response.Headers["RateLimit-Limit"] = "200";
        context.Response.Headers["RateLimit-Remaining"] = "0";
        context.Response.Headers["RateLimit-Reset"] = context.Response.Headers["Retry-After"];
    }
});
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// ===========================================
// Endpoint Mapping
// ===========================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
app.MapHub<NotificationsHub>(NotificationsHub.Path);
app.MapHub<ChatHub>(ChatHub.Path);

// Health check endpoint
app.MapHealthChecks("/health");

// ===========================================
// Run Application
// ===========================================

app.Run();

// Make the implicit Program class accessible to integration tests
public partial class Program { }
