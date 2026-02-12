using KasahQMS.Domain.Entities.Identity;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for generating and validating JWT tokens.
/// </summary>
public interface ITokenService
{
    Task<string> GenerateAccessToken(User user, CancellationToken cancellationToken = default);
    Task<string> GenerateRefreshToken(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ValidateRefreshToken(string refreshToken, CancellationToken cancellationToken = default);
    Task<Guid?> ValidateAccessToken(string accessToken, CancellationToken cancellationToken = default);
}
