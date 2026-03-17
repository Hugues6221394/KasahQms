using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

public class ResetPasswordModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordModel> _logger;

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
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = "Missing reset token. Please request a new password reset link.";
            return;
        }

        // Validate token exists in DB and is not expired
        var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == Token &&
            u.PasswordResetTokenExpiry != null &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        TokenValid = user != null;
        if (!TokenValid)
            ErrorMessage = "This reset link has expired or is invalid. Please request a new one.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = "Missing reset token.";
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

        // Look up user by DB-persisted token (also re-checks expiry)
        var user = await _dbContext.Users.FirstOrDefaultAsync(u =>
            u.PasswordResetToken == Token &&
            u.PasswordResetTokenExpiry != null &&
            u.PasswordResetTokenExpiry > DateTime.UtcNow);

        if (user == null)
        {
            ErrorMessage = "This reset link has expired or is invalid. Please request a new one.";
            TokenValid = false;
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

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for user {UserId}", user.Id);
        Success = true;
        return Page();
    }
}
