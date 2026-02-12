using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository implementation for UserPermissionDelegation entity.
/// </summary>
public class UserPermissionDelegationRepository : BaseRepository<UserPermissionDelegation>, IUserPermissionDelegationRepository
{
    public UserPermissionDelegationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<UserPermissionDelegation>> GetByDelegatorIdAsync(
        Guid delegatorId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.DelegatedById == delegatorId && d.IsActive && (d.ExpiresAt == null || d.ExpiresAt > DateTime.UtcNow))
            .Include(d => d.User)
            .OrderByDescending(d => d.DelegatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserPermissionDelegation>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.UserId == userId && d.IsActive && (d.ExpiresAt == null || d.ExpiresAt > DateTime.UtcNow))
            .Include(d => d.DelegatedBy)
            .OrderByDescending(d => d.DelegatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<UserPermissionDelegation?> GetByUserAndPermissionAsync(
        Guid userId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.UserId == userId && 
                       d.Permission == permission && 
                       d.IsActive && 
                       (d.ExpiresAt == null || d.ExpiresAt > DateTime.UtcNow))
            .Include(d => d.DelegatedBy)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserPermissionDelegation>> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.TenantId == tenantId && d.IsActive && (d.ExpiresAt == null || d.ExpiresAt > DateTime.UtcNow))
            .Include(d => d.User)
            .Include(d => d.DelegatedBy)
            .OrderByDescending(d => d.DelegatedAt)
            .ToListAsync(cancellationToken);
    }
}

