using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Identity.Dtos;
using KasahQMS.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Identity.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResponseDto>>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponseDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result<AuthResponseDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Login attempt for non-existent user: {Email}", request.Email);
                return Result.Failure<AuthResponseDto>(Error.NotFound);
            }

            if (!user.IsActive)
            {
                await _auditLogService.LogAuthenticationAsync(
                    user.Id,
                    "LOGIN_FAILED",
                    "Account inactive",
                    _currentUserService.IpAddress,
                    _currentUserService.UserAgent,
                    false,
                    cancellationToken);

                return Result.Failure<AuthResponseDto>(Error.Unauthorized);
            }

            if (user.IsLockedOut)
            {
                await _auditLogService.LogAuthenticationAsync(
                    user.Id,
                    "LOGIN_FAILED",
                    "Account locked",
                    _currentUserService.IpAddress,
                    _currentUserService.UserAgent,
                    false,
                    cancellationToken);

                return Result.Failure<AuthResponseDto>(Error.Forbidden);
            }

            var passwordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);

            if (!passwordValid)
            {
                user.RecordFailedLogin();
                await _userRepository.UpdateAsync(user, cancellationToken);

                await _auditLogService.LogAuthenticationAsync(
                    user.Id,
                    "LOGIN_FAILED",
                    "Invalid password",
                    _currentUserService.IpAddress,
                    _currentUserService.UserAgent,
                    false,
                    cancellationToken);

                _logger.LogWarning("Invalid password for user: {Email}", request.Email);
                return Result.Failure<AuthResponseDto>(Error.Unauthorized);
            }

            // Generate tokens
            var accessToken = await _tokenService.GenerateAccessToken(user, cancellationToken);
            var refreshToken = await _tokenService.GenerateRefreshToken(user.Id, cancellationToken);

            // Record successful login
            user.RecordSuccessfulLogin();
            await _userRepository.UpdateAsync(user, cancellationToken);

            await _auditLogService.LogAuthenticationAsync(
                user.Id,
                "LOGIN_SUCCESS",
                "User logged in successfully",
                _currentUserService.IpAddress,
                _currentUserService.UserAgent,
                true,
                cancellationToken);

            var response = new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
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
            _logger.LogError(ex, "Error during login for {Email}", request.Email);
            return Result.Failure<AuthResponseDto>(Error.Custom("Login.Failed", "An error occurred during login."));
        }
    }
}
