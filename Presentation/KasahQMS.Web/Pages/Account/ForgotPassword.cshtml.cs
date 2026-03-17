using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordModel> _logger;
    private readonly IWebHostEnvironment _env;

    public ForgotPasswordModel(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        ILogger<ForgotPasswordModel> logger,
        IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
        _env = env;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool Submitted { get; set; }
    public bool IsDevelopmentMode => _env.IsDevelopment();
    public string? DevResetLink { get; set; }

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
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == Email.ToLower() && u.IsActive);

            if (user != null)
            {
                // Generate a secure URL-safe token
                var rawBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                var token = Convert.ToBase64String(rawBytes)
                    .Replace("+", "-").Replace("/", "_").Replace("=", "");

                // Store in DB — survives server restarts
                user.PasswordResetToken = token;
                user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
                await _dbContext.SaveChangesAsync();

                var resetLink = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={Uri.EscapeDataString(token)}";

                // Always log the link so it's accessible even when email fails
                _logger.LogInformation(
                    "[PASSWORD RESET] Link for {Email}: {ResetLink}", user.Email, resetLink);

                await _emailService.SendEmailAsync(
                    user.Email,
                    "Password Reset Request – KASAH QMS",
                    $"<p>Hello {user.FirstName},</p>" +
                    $"<p>We received a request to reset your password. Click the link below:</p>" +
                    $"<p><a href=\"{resetLink}\" style=\"background:#0c88e8;color:white;padding:10px 22px;border-radius:5px;text-decoration:none;\">Reset My Password</a></p>" +
                    $"<p>This link expires in 1 hour. If you did not request this, ignore this email.</p>" +
                    $"<p>— KASAH QMS Team</p>",
                    isHtml: true);

                // In dev, expose the link directly on the page so you never get stuck
                if (_env.IsDevelopment())
                    DevResetLink = resetLink;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset for {Email}", Email);
        }

        Submitted = true;
        return Page();
    }
}
