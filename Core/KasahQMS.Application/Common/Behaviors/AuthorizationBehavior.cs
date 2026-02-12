using KasahQMS.Application.Common.Security;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Common.Behaviors;

/// <summary>
/// Authorization behavior for MediatR pipeline.
/// Checks permissions before executing handlers.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<AuthorizationBehavior<TRequest, TResponse>> _logger;

    public AuthorizationBehavior(
        IAuthorizationService authorizationService,
        ILogger<AuthorizationBehavior<TRequest, TResponse>> logger)
    {
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check for AuthorizeAttribute on the request
        var authorizeAttribute = typeof(TRequest)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .FirstOrDefault();

        if (authorizeAttribute != null)
        {
            // Check permissions
            if (!string.IsNullOrEmpty(authorizeAttribute.Permissions))
            {
                var permissions = authorizeAttribute.Permissions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList();

                bool hasPermission;

                if (authorizeAttribute.RequireAll)
                {
                    hasPermission = await _authorizationService.HasAllPermissionsAsync(permissions, cancellationToken);
                }
                else
                {
                    hasPermission = await _authorizationService.HasAnyPermissionAsync(permissions, cancellationToken);
                }

                if (!hasPermission)
                {
                    _logger.LogWarning(
                        "Authorization failed for request {RequestType} - missing required permissions: {Permissions}",
                        typeof(TRequest).Name,
                        string.Join(", ", permissions));

                    throw new UnauthorizedAccessException(
                        $"Permission denied. Required: {string.Join(" or ", permissions)}");
                }
            }

            // Check roles
            if (!string.IsNullOrEmpty(authorizeAttribute.Roles))
            {
                var roles = authorizeAttribute.Roles
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .ToList();

                var hasRole = false;
                foreach (var role in roles)
                {
                    if (await _authorizationService.IsInRoleAsync(role, cancellationToken))
                    {
                        hasRole = true;
                        break;
                    }
                }

                if (!hasRole)
                {
                    _logger.LogWarning(
                        "Authorization failed for request {RequestType} - missing required role: {Roles}",
                        typeof(TRequest).Name,
                        string.Join(", ", roles));

                    throw new UnauthorizedAccessException(
                        $"Role denied. Required: {string.Join(" or ", roles)}");
                }
            }
        }

        return await next();
    }
}

