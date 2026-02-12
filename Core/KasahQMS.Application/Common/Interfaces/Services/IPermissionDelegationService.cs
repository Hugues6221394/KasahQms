using KasahQMS.Application.Common.Models;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for managing permission delegations from managers to subordinates.
/// Enforces bounded delegation: users can only delegate permissions they possess.
/// </summary>
public interface IPermissionDelegationService
{
    /// <summary>
    /// Delegates a permission from the current user to a subordinate.
    /// </summary>
    /// <param name="subordinateId">The user receiving the delegated permission</param>
    /// <param name="permission">The permission string to delegate (e.g., "Documents.Create")</param>
    /// <param name="expiresAfterDays">Optional expiration in days</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure with error details</returns>
    Task<Result> DelegatePermissionAsync(
        Guid subordinateId,
        string permission,
        int? expiresAfterDays = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a delegated permission.
    /// </summary>
    /// <param name="delegationId">The ID of the delegation to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> RevokeDelegationAsync(
        Guid delegationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active delegations for the current user (as delegator).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active delegations</returns>
    Task<IEnumerable<DelegationDto>> GetMyDelegationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active delegations received by the current user (as delegatee).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active delegations</returns>
    Task<IEnumerable<DelegationDto>> GetReceivedDelegationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user can delegate a specific permission to a subordinate.
    /// </summary>
    /// <param name="subordinateId">The target subordinate</param>
    /// <param name="permission">The permission to delegate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if delegation is allowed, false otherwise</returns>
    Task<bool> CanDelegatePermissionAsync(
        Guid subordinateId,
        string permission,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active delegated permissions for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Set of permission strings</returns>
    Task<IEnumerable<string>> GetDelegatedPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO for permission delegation information.
/// </summary>
public class DelegationDto
{
    public Guid Id { get; set; }
    public Guid SubordinateId { get; set; }
    public string SubordinateName { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime DelegatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
}

