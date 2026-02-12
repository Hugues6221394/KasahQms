using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Services;

/// <summary>
/// Service for managing user hierarchy and visibility.
/// </summary>
public class HierarchyService : IHierarchyService
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<HierarchyService> _logger;

    public HierarchyService(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        ILogger<HierarchyService> logger)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IEnumerable<Guid>> GetSubordinateIdsAsync(
        Guid managerId, 
        bool recursive = true, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subordinates = await _userRepository.GetSubordinatesAsync(managerId, recursive, cancellationToken);
            return subordinates.Select(u => u.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subordinate IDs for manager {ManagerId}", managerId);
            return Enumerable.Empty<Guid>();
        }
    }

    public async Task<IEnumerable<Guid>> GetVisibleUserIdsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Start with the user themselves
            var visibleIds = new HashSet<Guid> { userId };

            // Check if user is a manager - if so, include all subordinates
            if (await IsManagerAsync(userId, cancellationToken))
            {
                var subordinateIds = await GetSubordinateIdsAsync(userId, recursive: true, cancellationToken);
                foreach (var id in subordinateIds)
                {
                    visibleIds.Add(id);
                }
            }

            return visibleIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting visible user IDs for user {UserId}", userId);
            return new[] { userId }; // Fallback to self only
        }
    }

    public async Task<bool> IsManagerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var subordinates = await _userRepository.GetSubordinatesAsync(userId, recursive: false, cancellationToken);
            return subordinates.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {UserId} is a manager", userId);
            return false;
        }
    }

    public async Task<bool> IsSubordinateAsync(
        Guid managerId, 
        Guid targetUserId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check direct relationship first
            var directSubordinates = await _userRepository.GetSubordinatesAsync(managerId, recursive: false, cancellationToken);
            if (directSubordinates.Any(s => s.Id == targetUserId))
            {
                return true;
            }

            // Check recursive relationship
            var allSubordinates = await _userRepository.GetSubordinatesAsync(managerId, recursive: true, cancellationToken);
            return allSubordinates.Any(s => s.Id == targetUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if user {TargetUserId} is subordinate of {ManagerId}", targetUserId, managerId);
            return false;
        }
    }

    public async Task<IEnumerable<Guid>> GetManagerChainAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chain = new List<Guid>();
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            
            if (user == null) return chain;

            var currentUser = user;
            var visited = new HashSet<Guid> { userId }; // Prevent cycles

            while (currentUser?.ManagerId != null && !visited.Contains(currentUser.ManagerId.Value))
            {
                visited.Add(currentUser.ManagerId.Value);
                chain.Add(currentUser.ManagerId.Value);
                
                currentUser = await _userRepository.GetByIdAsync(currentUser.ManagerId.Value, cancellationToken);
                if (currentUser == null) break;
            }

            return chain;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting manager chain for user {UserId}", userId);
            return Enumerable.Empty<Guid>();
        }
    }

    public async Task<IEnumerable<Guid>> GetDepartmentUserIdsAsync(
        Guid departmentId, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _userRepository.GetUserIdsInOrganizationUnitAsync(departmentId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user IDs for department {DepartmentId}", departmentId);
            return Enumerable.Empty<Guid>();
        }
    }
}

