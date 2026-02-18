using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
    }

    public async Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(rt => rt.UserId == userId).ToListAsync(cancellationToken);
    }

    public async Task RevokeByUserIdAsync(Guid userId, string reason, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        var tokens = await _dbSet
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(ipAddress, reason);
            _dbSet.Update(token);
        }
    }
}
