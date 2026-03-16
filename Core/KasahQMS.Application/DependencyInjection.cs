using System.Reflection;
using FluentValidation;
using KasahQMS.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace KasahQMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR
        services.AddMediatR(cfg => {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        // Register validators
        services.AddValidatorsFromAssembly(assembly);

        // Register pipeline behaviors
        // Order matters (outermost to innermost):
        // 1. Logging - logs all requests including failures
        // 2. Validation - validates before auth to avoid wasted DB lookups
        // 3. Authorization - checks permissions
        // 4. Transaction - wraps handler in DB transaction (commands only)
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return services;
    }
}
