using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Identity.Commands;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword, string ConfirmPassword, bool IsFirstLogin = false) : IRequest<Result>;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService,
        IAuditLogService auditLogService,
        IUnitOfWork unitOfWork,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId == null)
            {
                return Result.Failure(Error.Unauthorized);
            }

            var user = await _userRepository.GetByIdAsync(userId.Value, cancellationToken);
            if (user == null)
            {
                return Result.Failure(Error.NotFound);
            }

            // Validate current password
            // If user requires password change (first login), skip current password check if requested
            bool skipCurrentPasswordCheck = request.IsFirstLogin && user.RequirePasswordChange;

            if (!skipCurrentPasswordCheck)
            {
                if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
                {
                    await _auditLogService.LogAuthenticationAsync(
                        user.Id,
                        "PASSWORD_CHANGE_FAILED",
                        "Invalid current password",
                        _currentUserService.IpAddress,
                        _currentUserService.UserAgent,
                        false,
                        cancellationToken);

                    return Result.Failure(Error.Custom("Password.Invalid", "Current password is incorrect."));
                }
            }

            // Validate new password (simple complexity check)
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return Result.Failure(Error.Custom("Password.TooShort", "Password must be at least 8 characters long."));
            }

            if (request.NewPassword != request.ConfirmPassword)
            {
                return Result.Failure(Error.Custom("Password.Mismatch", "Passwords do not match."));
            }

            var newHash = _passwordHasher.Hash(request.NewPassword);
            user.ChangePassword(newHash);

            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAuthenticationAsync(
                user.Id,
                "PASSWORD_CHANGE_SUCCESS",
                "User changed password successfully",
                _currentUserService.IpAddress,
                _currentUserService.UserAgent,
                true,
                cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", _currentUserService.UserId);
            return Result.Failure(Error.Custom("Password.ChangeFailed", "Failed to change password."));
        }
    }
}
