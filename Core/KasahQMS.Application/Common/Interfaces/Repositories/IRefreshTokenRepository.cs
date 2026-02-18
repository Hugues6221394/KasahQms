using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for RefreshToken entities.
/// </summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IEnumerable<RefreshToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RevokeByUserIdAsync(Guid userId, string reason, string? ipAddress = null, CancellationToken cancellationToken = default);
}
