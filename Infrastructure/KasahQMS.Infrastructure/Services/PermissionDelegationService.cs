using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Common.Models;
using KasahQMS.Application.Common.Security;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Service for managing permission delegations with bounded delegation rules.
/// </summary>
public class PermissionDelegationService : IPermissionDelegationService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserPermissionDelegationRepository _delegationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IHierarchyService _hierarchyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PermissionDelegationService> _logger;

    public PermissionDelegationService(
        ICurrentUserService currentUserService,
        IUserPermissionDelegationRepository delegationRepository,
        IUserRepository userRepository,
        IHierarchyService hierarchyService,
        IUnitOfWork unitOfWork,
        ILogger<PermissionDelegationService> logger)
    {
        _currentUserService = currentUserService;
        _delegationRepository = delegationRepository;
        _userRepository = userRepository;
        _hierarchyService = hierarchyService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> DelegatePermissionAsync(
        Guid subordinateId,
        string permission,
        int? expiresAfterDays = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var delegatorId = _currentUserService.UserId;
            var tenantId = _currentUserService.TenantId;

            if (delegatorId == null || tenantId == null)
            {
                return Result.Failure(Error.Unauthorized);
            }

            // Validate delegation is allowed
            var canDelegate = await CanDelegatePermissionAsync(subordinateId, permission, cancellationToken);
            if (!canDelegate)
            {
                return Result.Failure(Error.Custom(
                    "Delegation.NotAllowed",
                    "You cannot delegate this permission. Ensure you have the permission and the target is your subordinate."));
            }

            // Check if delegation already exists
            var existing = await _delegationRepository.GetByUserAndPermissionAsync(
                subordinateId,
                permission,
                cancellationToken);

            if (existing != null)
            {
                // Update existing delegation
                existing.IsActive = true;
                existing.ExpiresAt = expiresAfterDays.HasValue
                    ? DateTime.UtcNow.AddDays(expiresAfterDays.Value)
                    : null;
                existing.LastModifiedAt = DateTime.UtcNow;
                existing.LastModifiedById = delegatorId;

                await _delegationRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                // Create new delegation
                var delegation = UserPermissionDelegation.Create(
                    tenantId.Value,
                    subordinateId,
                    delegatorId.Value,
                    permission,
                    expiresAfterDays,
                    delegatorId.Value);

                await _delegationRepository.AddAsync(delegation, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Permission {Permission} delegated from {DelegatorId} to {SubordinateId}",
                permission,
                delegatorId,
                subordinateId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delegate permission {Permission} to {SubordinateId}", permission, subordinateId);
            return Result.Failure(Error.Custom("Delegation.Failed", "Failed to delegate permission."));
        }
    }

    public async Task<Result> RevokeDelegationAsync(
        Guid delegationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var currentUserId = _currentUserService.UserId;
            if (currentUserId == null)
            {
                return Result.Failure(Error.Unauthorized);
            }

            var delegation = await _delegationRepository.GetByIdAsync(delegationId, cancellationToken);
            if (delegation == null)
            {
                return Result.Failure(Error.Custom("Delegation.NotFound", "Delegation not found."));
            }

            // Only the delegator can revoke
            if (delegation.DelegatedById != currentUserId.Value)
            {
                return Result.Failure(Error.Custom("Delegation.Forbidden", "Only the delegator can revoke this delegation."));
            }

            delegation.Deactivate();
            delegation.LastModifiedById = currentUserId.Value;

            await _delegationRepository.UpdateAsync(delegation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Delegation {DelegationId} revoked by {UserId}", delegationId, currentUserId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke delegation {DelegationId}", delegationId);
            return Result.Failure(Error.Custom("Delegation.RevokeFailed", "Failed to revoke delegation."));
        }
    }

    public async Task<IEnumerable<DelegationDto>> GetMyDelegationsAsync(CancellationToken cancellationToken = default)
    {
        var delegatorId = _currentUserService.UserId;
        if (delegatorId == null)
        {
            return Enumerable.Empty<DelegationDto>();
        }

        var delegations = await _delegationRepository.GetByDelegatorIdAsync(delegatorId.Value, cancellationToken);

        return delegations.Select(d => new DelegationDto
        {
            Id = d.Id,
            SubordinateId = d.UserId,
            SubordinateName = d.User?.FullName ?? "Unknown",
            Permission = d.Permission,
            DelegatedAt = d.DelegatedAt,
            ExpiresAt = d.ExpiresAt,
            IsActive = d.IsActive,
            IsExpired = d.IsExpired
        });
    }

    public async Task<IEnumerable<DelegationDto>> GetReceivedDelegationsAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
        {
            return Enumerable.Empty<DelegationDto>();
        }

        var delegations = await _delegationRepository.GetByUserIdAsync(userId.Value, cancellationToken);

        return delegations.Select(d => new DelegationDto
        {
            Id = d.Id,
            SubordinateId = d.UserId,
            SubordinateName = d.DelegatedBy?.FullName ?? "Unknown",
            Permission = d.Permission,
            DelegatedAt = d.DelegatedAt,
            ExpiresAt = d.ExpiresAt,
            IsActive = d.IsActive,
            IsExpired = d.IsExpired
        });
    }

    public async Task<bool> CanDelegatePermissionAsync(
        Guid subordinateId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var delegatorId = _currentUserService.UserId;
        if (delegatorId == null)
        {
            return false;
        }

        // Rule 1: Delegator must have the permission themselves
        // Get permissions directly from user roles to avoid circular dependency
        var user = await _userRepository.GetByIdWithRolesAsync(delegatorId.Value, cancellationToken);
        if (user == null || user.Roles == null)
        {
            return false;
        }

        var userPermissions = new HashSet<string>();
        foreach (var role in user.Roles)
        {
            if (role.Permissions != null)
            {
                foreach (var rolePermission in role.Permissions)
                {
                    userPermissions.Add(rolePermission.ToString());
                }
            }
        }

        // Also check delegated permissions
        var delegatedPermissions = await GetDelegatedPermissionsAsync(delegatorId.Value, cancellationToken);
        foreach (var delegatedPermission in delegatedPermissions)
        {
            userPermissions.Add(delegatedPermission);
        }

        var hasPermission = userPermissions.Contains(permission);
        if (!hasPermission)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delegate permission {Permission} but does not have it",
                delegatorId,
                permission);
            return false;
        }

        // Rule 2: Target must be a subordinate (downward delegation only)
        var isSubordinate = await _hierarchyService.IsSubordinateAsync(delegatorId.Value, subordinateId, cancellationToken);
        if (!isSubordinate)
        {
            _logger.LogWarning(
                "User {UserId} attempted to delegate to {SubordinateId} who is not a subordinate",
                delegatorId,
                subordinateId);
            return false;
        }

        // Rule 3: Cannot delegate to self
        if (delegatorId.Value == subordinateId)
        {
            return false;
        }

        return true;
    }

    public async Task<IEnumerable<string>> GetDelegatedPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var delegations = await _delegationRepository.GetByUserIdAsync(userId, cancellationToken);
            return delegations
                .Where(d => d.IsValid)
                .Select(d => d.Permission)
                .Distinct();
        }
        catch (Exception ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("relation"))
        {
            // Table doesn't exist yet - return empty list
            _logger.LogWarning("Permission delegation table not found - returning empty delegated permissions.");
            return Enumerable.Empty<string>();
        }
    }
}

