using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Repositories;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Application.Features.Identity.Commands;

public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    bool IsFirstLogin = false) : IRequest<Result>;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _currentUserService = currentUserService;
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

            // Verify current password (skip if first login)
            if (!request.IsFirstLogin && !_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return Result.Failure(Error.Custom("Password.Invalid", "Current password is incorrect."));
            }

            // Validate new password strength
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return Result.Failure(Error.Custom("Password.Weak", "Password must be at least 8 characters long."));
            }

            // Change password
            var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.ChangePassword(newPasswordHash);
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogAsync(
                "PASSWORD_CHANGED",
                "Users",
                user.Id,
                request.IsFirstLogin ? "Password changed on first login" : "Password changed",
                cancellationToken);

            _logger.LogInformation("Password changed for user {UserId}, IsFirstLogin: {IsFirstLogin}", userId, request.IsFirstLogin);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password for user {UserId}", _currentUserService.UserId);
            return Result.Failure(Error.Custom("Password.ChangeFailed", "Failed to change password."));
        }
    }
}

