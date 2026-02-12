using System.Linq;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Implementation of authorization service with caching for performance.
/// </summary>
public class AuthorizationService : IAuthorizationService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICacheService _cacheService;
    private readonly IPermissionDelegationService? _permissionDelegationService;
    private readonly IHierarchyService? _hierarchyService;
    private readonly ILogger<AuthorizationService> _logger;

    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);

    public AuthorizationService(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICacheService cacheService,
        ILogger<AuthorizationService> logger,
        IPermissionDelegationService? permissionDelegationService = null,
        IHierarchyService? hierarchyService = null)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _cacheService = cacheService;
        _permissionDelegationService = permissionDelegationService;
        _hierarchyService = hierarchyService;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(cancellationToken);
        return permissions.Contains(permission);
    }

    public async Task<bool> HasAnyPermissionAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        var userPermissions = await GetUserPermissionsAsync(cancellationToken);
        return permissions.Any(p => userPermissions.Contains(p));
    }

    public async Task<bool> HasAllPermissionsAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default)
    {
        var userPermissions = await GetUserPermissionsAsync(cancellationToken);
        return permissions.All(p => userPermissions.Contains(p));
    }

    public async Task<bool> IsInRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return false;

        var cacheKey = $"user_roles_{userId}";
        var roles = await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await GetUserRolesFromDbAsync(userId.Value, cancellationToken),
            CacheExpiration,
            cancellationToken);

        return roles?.Contains(role) ?? false;
    }

    public async Task<bool> CanAccessResourceAsync(string resourceType, Guid resourceId, string action, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return false;

        // Build the permission string
        var permission = $"{resourceType}.{action}";

        // First check if user has the general permission
        if (await HasPermissionAsync(permission, cancellationToken))
        {
            return true;
        }

        // Check for ViewAll permission for read actions
        if (action == "View" && await HasPermissionAsync($"{resourceType}.ViewAll", cancellationToken))
        {
            return true;
        }

        return false;
    }

    public async Task<bool> CanViewUserDataAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        // Users can always view their own data
        if (currentUserId == targetUserId) return true;

        // Check if user has ViewAll permission
        if (await HasPermissionAsync(Permissions.Users.ViewAll, cancellationToken))
        {
            return true;
        }

        // Check hierarchy: can view if target is a subordinate
        if (_hierarchyService != null)
        {
            var isSubordinate = await _hierarchyService.IsSubordinateAsync(currentUserId.Value, targetUserId, cancellationToken);
            if (isSubordinate)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> CanViewSubordinateDataAsync(Guid subordinateId, CancellationToken cancellationToken = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        // Users can always view their own data
        if (currentUserId == subordinateId) return true;

        // Must be a manager with ViewAll permission or hierarchy check
        var hasViewAll = await HasPermissionAsync(Permissions.Users.ViewAll, cancellationToken);
        if (!hasViewAll)
        {
            return false;
        }

        // Verify the target is actually a subordinate
        if (_hierarchyService != null)
        {
            return await _hierarchyService.IsSubordinateAsync(currentUserId.Value, subordinateId, cancellationToken);
        }

        // If no hierarchy service, fall back to ViewAll permission only
        return hasViewAll;
    }

    public async Task<bool> CanDelegatePermissionAsync(Guid subordinateId, string permission, CancellationToken cancellationToken = default)
    {
        // Use the permission delegation service if available
        if (_permissionDelegationService != null)
        {
            return await _permissionDelegationService.CanDelegatePermissionAsync(subordinateId, permission, cancellationToken);
        }

        // Fallback: basic checks
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null) return false;

        // Must have the permission to delegate it
        if (!await HasPermissionAsync(permission, cancellationToken))
        {
            return false;
        }

        // Must be delegating to a subordinate
        if (_hierarchyService != null)
        {
            return await _hierarchyService.IsSubordinateAsync(currentUserId.Value, subordinateId, cancellationToken);
        }

        return false;
    }

    public async Task<IEnumerable<string>> GetUserPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null) return Enumerable.Empty<string>();

        var cacheKey = $"user_permissions_{userId}";
        var permissions = await _cacheService.GetOrCreateAsync(
            cacheKey,
            async () => await GetUserPermissionsFromDbAsync(userId.Value, cancellationToken),
            CacheExpiration,
            cancellationToken);

        return permissions ?? Enumerable.Empty<string>();
    }

    public async Task AuthorizeAsync(string permission, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(permission, cancellationToken))
        {
            _logger.LogWarning(
                "Authorization failed for user {UserId} - missing permission: {Permission}",
                _currentUserService.UserId,
                permission);

            throw new UnauthorizedAccessException($"Permission denied: {permission}");
        }
    }

    private async Task<IEnumerable<string>> GetUserPermissionsFromDbAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);
            if (user == null) return Enumerable.Empty<string>();

            var permissions = new HashSet<string>();
            var allRolePermissions = new List<Permission>();
            var roleNames = new List<string>();

            // Get permissions from all roles (map Domain enum -> Application permission strings)
            if (user.Roles != null)
            {
                foreach (var role in user.Roles)
                {
                    if (!string.IsNullOrEmpty(role.Name))
                        roleNames.Add(role.Name);
                    if (role.Permissions != null)
                    {
                        foreach (var perm in role.Permissions)
                        {
                            allRolePermissions.Add(perm);
                            foreach (var appPerm in PermissionMapper.ToApplicationPermissions(perm))
                                permissions.Add(appPerm);
                        }
                    }
                }
            }

            // Hierarchy roles (TMD, Deputy, Department Manager): add ViewAll for Read permissions
            var isHierarchyRole = roleNames.Any(rn =>
                string.Equals(rn, "TMD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rn, "Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rn, "Country Manager", StringComparison.OrdinalIgnoreCase) ||
                (rn != null && rn.Contains("Deputy", StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(rn, "Department Manager", StringComparison.OrdinalIgnoreCase));
            if (isHierarchyRole && allRolePermissions.Count > 0)
            {
                var hasDocumentRead = allRolePermissions.Any(p => (p & Permission.DocumentRead) != 0);
                var hasTaskRead = allRolePermissions.Any(p => (p & Permission.TaskRead) != 0);
                var hasAuditRead = allRolePermissions.Any(p => (p & Permission.AuditRead) != 0);
                var hasCapaRead = allRolePermissions.Any(p => (p & Permission.CapaRead) != 0);
                var hasUserRead = allRolePermissions.Any(p => (p & Permission.UserRead) != 0);
                foreach (var v in PermissionMapper.ViewAllForHierarchyRoles(hasDocumentRead, hasTaskRead, hasAuditRead, hasCapaRead, hasUserRead))
                    permissions.Add(v);
            }

            // Add delegated permissions if delegation service is available
            // Gracefully handle if table doesn't exist yet (during initial setup)
            if (_permissionDelegationService != null)
            {
                try
                {
                    var delegatedPermissions = await _permissionDelegationService.GetDelegatedPermissionsAsync(
                        userId,
                        cancellationToken);
                    
                    foreach (var delegatedPermission in delegatedPermissions)
                    {
                        permissions.Add(delegatedPermission);
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("relation"))
                {
                    // Table doesn't exist yet - this is OK during initial setup
                    // Log but don't fail - permissions from roles will still work
                    _logger.LogWarning("Permission delegation table not found - skipping delegated permissions. Run database migrations to enable delegation.");
                }
            }

            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permissions for user {UserId}", userId);
            return Enumerable.Empty<string>();
        }
    }

    private async Task<IEnumerable<string>> GetUserRolesFromDbAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdWithRolesAsync(userId, cancellationToken);
            if (user?.Roles == null) return Enumerable.Empty<string>();

            return user.Roles.Select(r => r.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get roles for user {UserId}", userId);
            return Enumerable.Empty<string>();
        }
    }
}
