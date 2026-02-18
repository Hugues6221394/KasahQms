using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Identity.Dtos;
using KasahQMS.Domain.Common;
using KasahQMS.Domain.Entities.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Identity.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponseDto>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IUnitOfWork unitOfWork,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AuthResponseDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var storedToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

            if (storedToken == null)
            {
                return Result.Failure<AuthResponseDto>(Error.Custom("Token.Invalid", "Invalid refresh token."));
            }

            if (storedToken.IsRevoked)
            {
                _logger.LogWarning("Attempted reuse of revoked refresh token {TokenId}", storedToken.Id);
                return Result.Failure<AuthResponseDto>(Error.Custom("Token.Revoked", "Refresh token has been revoked."));
            }

            if (storedToken.IsExpired)
            {
                return Result.Failure<AuthResponseDto>(Error.Custom("Token.Expired", "Refresh token has expired."));
            }

            var user = storedToken.User;
            if (user == null)
            {
                user = await _userRepository.GetByIdWithRolesAsync(storedToken.UserId, cancellationToken);
                if (user == null)
                {
                    return Result.Failure<AuthResponseDto>(Error.NotFound);
                }
            }

            if (!user.IsActive || user.IsLockedOut)
            {
                return Result.Failure<AuthResponseDto>(Error.Forbidden);
            }

            // Generate new tokens
            var newAccessToken = await _tokenService.GenerateAccessToken(user, cancellationToken);
            var newRefreshTokenString = await _tokenService.GenerateRefreshToken(user.Id, cancellationToken);

            // Create new refresh token entity
            var newRefreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = newRefreshTokenString,
                UserId = user.Id,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);

            // Revoke old token (Rotation)
            storedToken.Revoke(reason: "Refreshed", replacedByToken: newRefreshTokenString);
            await _refreshTokenRepository.UpdateAsync(storedToken, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new AuthResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenString,
                ExpiresAt = DateTime.UtcNow.AddMinutes(60),
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                RequirePasswordChange = user.RequirePasswordChange
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return Result.Failure<AuthResponseDto>(Error.Custom("Token.RefreshFailed", "Failed to refresh token."));
        }
    }
}
