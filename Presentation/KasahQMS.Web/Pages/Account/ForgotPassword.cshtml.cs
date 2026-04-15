using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class ForgotPasswordModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordModel> _logger;
    private readonly IMemoryCache _memoryCache;
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);

    public ForgotPasswordModel(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        ILogger<ForgotPasswordModel> logger,
        IMemoryCache memoryCache)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool Submitted { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ModelState.AddModelError(nameof(Email), "Email is required.");
            return Page();
        }

        try
        {
            var normalizedEmail = Email.Trim().ToLowerInvariant();
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.IsActive && !u.IsDeleted);

            if (user != null)
            {
                var now = DateTime.UtcNow;
                var cooldownKey = $"pwdreset:cooldown:{user.Id}";
                var shouldThrottle = _memoryCache.TryGetValue(cooldownKey, out _);

                if (!shouldThrottle)
                {
                    var token = GenerateSecureToken();
                    user.PasswordResetToken = token;
                    user.PasswordResetTokenExpiry = now.Add(ResetTokenLifetime);
                    await _dbContext.SaveChangesAsync();

                    await _emailService.SendPasswordResetEmailAsync(
                        user.Email,
                        user.FullName,
                        token);

                    _memoryCache.Set(cooldownKey, true, ResendCooldown);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset for {Email}", Email);
        }

        Submitted = true;
        return Page();
    }

    private static string GenerateSecureToken()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
