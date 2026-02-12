using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for User entities.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetSubordinatesAsync(Guid managerId, bool recursive = false, CancellationToken cancellationToken = default);
    Task<IEnumerable<Guid>> GetUserIdsInOrganizationUnitAsync(Guid orgUnitId, CancellationToken cancellationToken = default);
}
