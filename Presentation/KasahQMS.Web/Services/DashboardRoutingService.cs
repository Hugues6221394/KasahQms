using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Identity;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Web.Services;

/// <summary>
/// Service for determining the correct dashboard route based on user roles.
/// </summary>
public class DashboardRoutingService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<DashboardRoutingService> _logger;

    public DashboardRoutingService(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ILogger<DashboardRoutingService> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the dashboard route for a specific user (e.g. right after login).
    /// Use this when the HTTP context does not yet have the signed-in user (same request as SignIn).
    /// </summary>
    public string GetDashboardRouteForUser(User user)
    {
        if (user?.Roles == null || !user.Roles.Any())
        {
            _logger.LogWarning("User {UserId} has no roles, redirecting to default dashboard", user?.Id);
            return "/Dashboard/Staff";
        }

        var roleNames = user.Roles.Select(r => r.Name).ToList();
        return ResolveRouteFromRoles(user.Id, roleNames);
    }

    /// <summary>
    /// Gets the dashboard route for the current user based on their roles (from HTTP context).
    /// Priority: System Admin > TMD/Country Manager > Deputy > Manager > Auditor > Staff
    /// </summary>
    public async Task<string> GetDashboardRouteAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            _logger.LogWarning("No user ID found, redirecting to default dashboard");
            return "/Dashboard/Staff";
        }

        try
        {
            var user = await _userRepository.GetByIdWithRolesAsync(userId.Value, cancellationToken);
            if (user == null || user.Roles == null || !user.Roles.Any())
            {
                _logger.LogWarning("User {UserId} has no roles, redirecting to default dashboard", userId);
                return "/Dashboard/Staff";
            }

            var roleNames = user.Roles.Select(r => r.Name).ToList();
            return ResolveRouteFromRoles(userId.Value, roleNames);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining dashboard route for user {UserId}", userId);
            return "/Dashboard/Staff";
        }
    }

    private string ResolveRouteFromRoles(Guid userId, List<string> roleNames)
    {

        // Priority order (highest first):
        // 1. System Admin / SystemAdministrator
        if (roleNames.Any(r => r != null && (r.Equals("SystemAdmin", StringComparison.OrdinalIgnoreCase) ||
                               r.Equals("System Admin", StringComparison.OrdinalIgnoreCase) ||
                               r.Equals("SystemAdministrator", StringComparison.OrdinalIgnoreCase) ||
                               r.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase))))
        {
            _logger.LogInformation("User {UserId} routed to Admin dashboard (roles: {Roles})", userId, string.Join(", ", roleNames));
            return "/Dashboard/Admin";
        }

        // 2. TMD / TopManagingDirector / Country Manager
        if (roleNames.Any(r => r.Equals("TopManagingDirector", StringComparison.OrdinalIgnoreCase) ||
                               r.Equals("TMD", StringComparison.OrdinalIgnoreCase) ||
                               r.Equals("Country Manager", StringComparison.OrdinalIgnoreCase) ||
                               (r != null && r.Contains("TMD", StringComparison.OrdinalIgnoreCase))))
        {
            _logger.LogInformation("User {UserId} routed to Executive dashboard (roles: {Roles})", userId, string.Join(", ", roleNames));
            return "/Dashboard/Executive";
        }

        // 3. Deputy / DeputyDirector
        if (roleNames.Any(r => r != null && (r.Equals("DeputyDirector", StringComparison.OrdinalIgnoreCase) ||
                               r.Equals("Deputy", StringComparison.OrdinalIgnoreCase) ||
                               r.Contains("Deputy", StringComparison.OrdinalIgnoreCase))))
        {
            _logger.LogInformation("User {UserId} routed to Executive dashboard (roles: {Roles})", userId, string.Join(", ", roleNames));
            return "/Dashboard/Executive";
        }

        // 4. Department Managers
        if (roleNames.Any(r => r != null && (r.Equals("DepartmentManager", StringComparison.OrdinalIgnoreCase) ||
                               (r.Contains("Manager", StringComparison.OrdinalIgnoreCase) &&
                                !r.Contains("System", StringComparison.OrdinalIgnoreCase) &&
                                !r.Contains("Top", StringComparison.OrdinalIgnoreCase)))))
        {
            _logger.LogInformation("User {UserId} routed to Manager dashboard (roles: {Roles})", userId, string.Join(", ", roleNames));
            return "/Dashboard/Manager";
        }

        // 5. Auditor
        if (roleNames.Any(r => r != null && r.Equals("Auditor", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogInformation("User {UserId} routed to Auditor dashboard (roles: {Roles})", userId, string.Join(", ", roleNames));
            return "/Dashboard/Auditor";
        }

        // 6. Default: Staff / JuniorStaff
        _logger.LogInformation("User {UserId} routed to Staff dashboard (roles: {Roles})", userId, string.Join(", ", roleNames));
        return "/Dashboard/Staff";
    }
}

