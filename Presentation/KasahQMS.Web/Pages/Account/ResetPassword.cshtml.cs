using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordModel> _logger;
    private const int MaxOtpAttempts = 5;
    private static readonly TimeSpan OtpLockoutDuration = TimeSpan.FromMinutes(10);

    public ResetPasswordModel(
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        ILogger<ResetPasswordModel> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string Token { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool TokenValid { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        TokenValid = !string.IsNullOrWhiteSpace(Token);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = "Missing password reset token.";
            TokenValid = true;
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Enter your account email.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 8)
        {
            ErrorMessage = "Password must be at least 8 characters.";
            TokenValid = true;
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            TokenValid = true;
            return Page();
        }

        var normalizedEmail = Email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        // Bind OTP check to both email + code to prevent cross-account token use.
        var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalizedEmail &&
            u.PasswordResetToken == Token &&
            u.PasswordResetTokenExpiry != null &&
            u.PasswordResetTokenExpiry > now);

        if (user == null)
        {
            var candidateUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
            if (candidateUser != null)
            {
                if (candidateUser.LockoutEndTime.HasValue && candidateUser.LockoutEndTime.Value > now)
                {
                    TokenValid = false;
                    ErrorMessage = $"Too many invalid reset attempts. Try again after {candidateUser.LockoutEndTime.Value:HH:mm} UTC.";
                    return Page();
                }

                candidateUser.FailedLoginAttempts += 1;
                if (candidateUser.FailedLoginAttempts >= MaxOtpAttempts)
                {
                    candidateUser.IsLockedOut = true;
                    candidateUser.LockoutEndTime = now.Add(OtpLockoutDuration);
                    candidateUser.PasswordResetToken = null;
                    candidateUser.PasswordResetTokenExpiry = null;
                    ErrorMessage = "Too many invalid reset attempts. Your reset has been locked. Request a new reset link after 10 minutes.";
                    TokenValid = false;
                }
                else
                {
                    var remaining = MaxOtpAttempts - candidateUser.FailedLoginAttempts;
                    ErrorMessage = $"Invalid or expired reset token. You have {remaining} attempt(s) remaining.";
                    TokenValid = true;
                }

                await _dbContext.SaveChangesAsync();
                return Page();
            }

            ErrorMessage = "Invalid request. Request a new reset link and try again.";
            TokenValid = false;
            return Page();
        }

        if (user.LockoutEndTime.HasValue && user.LockoutEndTime.Value > now)
        {
            TokenValid = false;
            ErrorMessage = $"Reset is temporarily locked due to invalid attempts. Try again after {user.LockoutEndTime.Value:HH:mm} UTC.";
            return Page();
        }

        // Apply new password and clear the token so it cannot be reused
        user.PasswordHash = _passwordHasher.Hash(NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.RequirePasswordChange = false;
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        user.FailedLoginAttempts = 0;
        user.IsLockedOut = false;
        user.LockoutEndTime = null;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for user {UserId}", user.Id);
        Success = true;
        return Page();
    }
}
