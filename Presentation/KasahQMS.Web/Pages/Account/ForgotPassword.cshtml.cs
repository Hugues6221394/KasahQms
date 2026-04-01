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
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _memoryCache;
    private const int ResetOtpLength = 6;
    private static readonly TimeSpan ResetOtpLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    public ForgotPasswordModel(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        ILogger<ForgotPasswordModel> logger,
        IWebHostEnvironment env,
        IMemoryCache memoryCache)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
        _env = env;
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
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail && u.IsActive);

            if (user != null)
            {
                var now = DateTime.UtcNow;
                var cooldownKey = $"pwdreset:cooldown:{user.Id}";
                var shouldThrottle = _memoryCache.TryGetValue(cooldownKey, out _);

                string token;
                if (user.PasswordResetTokenExpiry.HasValue &&
                    user.PasswordResetTokenExpiry.Value > now &&
                    !string.IsNullOrWhiteSpace(user.PasswordResetToken))
                {
                    token = user.PasswordResetToken!;
                }
                else
                {
                    token = GenerateNumericOtp(ResetOtpLength);
                    user.PasswordResetToken = token;
                    user.PasswordResetTokenExpiry = now.Add(ResetOtpLifetime);
                    await _dbContext.SaveChangesAsync();
                }

                if (!shouldThrottle)
                {
                    if (_env.IsDevelopment())
                    {
                        _logger.LogInformation(
                            "[PASSWORD RESET][DEV] OTP for {Email}: {OtpCode}", user.Email, token);
                    }

                    await _emailService.SendPasswordResetOtpEmailAsync(
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

    private static string GenerateNumericOtp(int length)
    {
        var max = (int)Math.Pow(10, length);
        var value = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, max);
        return value.ToString($"D{length}");
    }
}
