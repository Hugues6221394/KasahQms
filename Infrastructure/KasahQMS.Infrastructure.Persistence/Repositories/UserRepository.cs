using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for User entity.
/// </summary>
public class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<User>> GetAllForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(u => u.TenantId == tenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetSubordinatesAsync(Guid managerId, bool recursive = false, CancellationToken cancellationToken = default)
    {
        var directReports = await _dbSet
            .Where(u => u.ManagerId == managerId)
            .ToListAsync(cancellationToken);

        if (!recursive)
        {
            return directReports;
        }

        var allSubordinates = new List<User>(directReports);
        foreach (var directReport in directReports)
        {
            var subordinates = await GetSubordinatesAsync(directReport.Id, true, cancellationToken);
            allSubordinates.AddRange(subordinates);
        }

        return allSubordinates;
    }

    public async Task<IEnumerable<Guid>> GetUserIdsInOrganizationUnitAsync(Guid orgUnitId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(u => u.OrganizationUnitId == orgUnitId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
