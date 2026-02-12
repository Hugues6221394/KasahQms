namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for managing user hierarchy and visibility.
/// </summary>
public interface IHierarchyService
{
    /// <summary>
    /// Gets all subordinate user IDs (recursive) for a manager.
    /// </summary>
    Task<IEnumerable<Guid>> GetSubordinateIdsAsync(Guid managerId, bool recursive = true, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all user IDs visible to the current user based on role and hierarchy.
    /// </summary>
    Task<IEnumerable<Guid>> GetVisibleUserIdsAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a user is a manager (has direct reports).
    /// </summary>
    Task<bool> IsManagerAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if target user is a subordinate of manager (recursive).
    /// </summary>
    Task<bool> IsSubordinateAsync(Guid managerId, Guid targetUserId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the manager chain (all managers up to root) for a user.
    /// </summary>
    Task<IEnumerable<Guid>> GetManagerChainAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all user IDs in a specific department.
    /// </summary>
    Task<IEnumerable<Guid>> GetDepartmentUserIdsAsync(Guid departmentId, CancellationToken cancellationToken = default);
}

