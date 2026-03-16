using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
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

    public void OnGet()
    {
        TokenValid = !string.IsNullOrWhiteSpace(Token);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorMessage = "Invalid or missing reset token.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ErrorMessage = "Password must be at least 6 characters.";
            TokenValid = true;
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            TokenValid = true;
            return Page();
        }

        var (valid, userId) = PasswordResetTokenStore.ValidateToken(Token);
        if (!valid || userId == Guid.Empty)
        {
            ErrorMessage = "This reset link has expired or is invalid. Please request a new one.";
            return Page();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            ErrorMessage = "User not found.";
            return Page();
        }

        user.PasswordHash = _passwordHasher.Hash(NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.RequirePasswordChange = false;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for user {UserId}", userId);
        Success = true;
        return Page();
    }
}
