using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Services;
using KasahQMS.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KasahQMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IDateTimeService, DateTimeService>();
        
        // Application services (from Persistence layer)
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<INotificationService, NotificationService>();
        
        // Hierarchy service
        services.AddScoped<IHierarchyService, HierarchyService>();
        
        // Permission delegation service
        services.AddScoped<IPermissionDelegationService, PermissionDelegationService>();
        
        // Authorization service
        services.AddScoped<KasahQMS.Application.Common.Security.IAuthorizationService, AuthorizationService>();
        
        // Other services
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddSingleton<ICacheService, CacheService>();

        return services;
    }
}
