using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

public class ForgotPasswordModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        ILogger<ForgotPasswordModel> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
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
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == Email.ToLower() && u.IsActive);

            if (user != null)
            {
                var token = PasswordResetTokenStore.CreateToken(user.Id);
                var resetLink = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={Uri.EscapeDataString(token)}";

                await _emailService.SendEmailAsync(
                    user.Email,
                    "Password Reset Request - KASAH QMS",
                    $"<p>Hello {user.FirstName},</p>" +
                    $"<p>We received a request to reset your password. Click the link below to set a new password:</p>" +
                    $"<p><a href=\"{resetLink}\">Reset My Password</a></p>" +
                    $"<p>This link expires in 1 hour. If you did not request this, you can safely ignore this email.</p>" +
                    $"<p>— KASAH QMS Team</p>",
                    isHtml: true);

                _logger.LogInformation("Password reset link sent to {Email}", Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending password reset email for {Email}", Email);
        }

        Submitted = true;
        return Page();
    }
}
