using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Security;

public class TwoFactorModel : PageModel
{
    private readonly ITwoFactorService _twoFactorService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public TwoFactorModel(
        ITwoFactorService twoFactorService,
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _twoFactorService = twoFactorService;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public bool IsEnabled { get; set; }
    public string? QrCodeUri { get; set; }
    public string? SecretKey { get; set; }
    public List<string>? RecoveryCodes { get; set; }
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public string? VerificationCode { get; set; }

    public async Task OnGetAsync()
    {
        var userId = GetUserId();
        if (userId == null) return;

        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        IsEnabled = user?.TwoFactorEnabled ?? false;
    }

    public async Task<IActionResult> OnPostSetupAsync()
    {
        var userId = GetUserId();
        if (userId == null) return RedirectToPage();

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return RedirectToPage();

        IsEnabled = user.TwoFactorEnabled;
        if (IsEnabled)
        {
            ErrorMessage = "Two-factor authentication is already enabled.";
            return Page();
        }

        SecretKey = _twoFactorService.GenerateSecretKey();
        QrCodeUri = _twoFactorService.GenerateQrCodeUri(user.Email, SecretKey);

        TempData["2fa_secret"] = SecretKey;
        return Page();
    }

    public async Task<IActionResult> OnPostEnableAsync()
    {
        var userId = GetUserId();
        if (userId == null) return RedirectToPage();

        var secret = TempData["2fa_secret"]?.ToString();
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(VerificationCode))
        {
            ErrorMessage = "Please complete the setup flow first.";
            return Page();
        }

        if (!_twoFactorService.ValidateCode(secret, VerificationCode))
        {
            ErrorMessage = "Invalid verification code. Please try again.";
            SecretKey = secret;
            var user2 = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user2 != null)
                QrCodeUri = _twoFactorService.GenerateQrCodeUri(user2.Email, secret);
            TempData["2fa_secret"] = secret;
            return Page();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return RedirectToPage();

        user.TwoFactorEnabled = true;
        user.TwoFactorSecret = secret;
        await _dbContext.SaveChangesAsync();

        RecoveryCodes = _twoFactorService.GenerateRecoveryCodes();
        IsEnabled = true;
        StatusMessage = "Two-factor authentication has been enabled successfully.";
        return Page();
    }

    public async Task<IActionResult> OnPostDisableAsync()
    {
        var userId = GetUserId();
        if (userId == null) return RedirectToPage();

        if (string.IsNullOrEmpty(VerificationCode))
        {
            ErrorMessage = "Please enter your current verification code to disable 2FA.";
            IsEnabled = true;
            return Page();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (user == null) return RedirectToPage();

        if (string.IsNullOrEmpty(user.TwoFactorSecret) ||
            !_twoFactorService.ValidateCode(user.TwoFactorSecret, VerificationCode))
        {
            ErrorMessage = "Invalid verification code.";
            IsEnabled = true;
            return Page();
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await _dbContext.SaveChangesAsync();

        IsEnabled = false;
        StatusMessage = "Two-factor authentication has been disabled.";
        return Page();
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }
}
