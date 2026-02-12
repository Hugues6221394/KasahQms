using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for UserPermissionDelegation entities.
/// </summary>
public interface IUserPermissionDelegationRepository : IRepository<UserPermissionDelegation>
{
    /// <summary>
    /// Gets all active delegations for a user (as delegator).
    /// </summary>
    Task<IEnumerable<UserPermissionDelegation>> GetByDelegatorIdAsync(
        Guid delegatorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active delegations received by a user (as delegatee).
    /// </summary>
    Task<IEnumerable<UserPermissionDelegation>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active delegations for a specific permission and user.
    /// </summary>
    Task<UserPermissionDelegation?> GetByUserAndPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active delegations for a tenant.
    /// </summary>
    Task<IEnumerable<UserPermissionDelegation>> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

