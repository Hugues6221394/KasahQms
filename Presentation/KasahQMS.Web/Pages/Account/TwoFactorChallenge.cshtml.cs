using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

[EnableRateLimiting("auth")]
public class TwoFactorChallengeModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITwoFactorService _twoFactorService;
    private readonly DashboardRoutingService _dashboardRoutingService;
    private readonly IAuditLoggingService _auditLoggingService;
    private readonly ISessionService _sessionService;
    private readonly ILogger<TwoFactorChallengeModel> _logger;

    public TwoFactorChallengeModel(
        ApplicationDbContext dbContext,
        ITwoFactorService twoFactorService,
        DashboardRoutingService dashboardRoutingService,
        IAuditLoggingService auditLoggingService,
        ISessionService sessionService,
        ILogger<TwoFactorChallengeModel> logger)
    {
        _dbContext = dbContext;
        _twoFactorService = twoFactorService;
        _dashboardRoutingService = dashboardRoutingService;
        _auditLoggingService = auditLoggingService;
        _sessionService = sessionService;
        _logger = logger;
    }

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        var pendingUserId = TempData.Peek("2fa_user_id")?.ToString();
        if (string.IsNullOrWhiteSpace(pendingUserId))
        {
            return RedirectToPage("/Account/Login", new { returnUrl });
        }

        ReturnUrl = returnUrl ?? TempData.Peek("2fa_return_url")?.ToString();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        var pendingUserIdRaw = TempData.Peek("2fa_user_id")?.ToString();
        if (!Guid.TryParse(pendingUserIdRaw, out var userId))
        {
            return RedirectToPage("/Account/Login", new { returnUrl });
        }

        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null || !user.IsActive || user.IsLockedOut || !user.TwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            await _auditLoggingService.LogActionAsync("LOGIN_2FA_INVALID_STATE", "User", userId, "Invalid 2FA login state", false);
            return RedirectToPage("/Account/Login", new { returnUrl });
        }

        if (string.IsNullOrWhiteSpace(Code) || !_twoFactorService.ValidateCode(user.TwoFactorSecret!, Code))
        {
            await _auditLoggingService.LogActionAsync("LOGIN_2FA_FAILED", "User", userId, "Invalid 2FA code", false);
            ErrorMessage = "Invalid authenticator code.";
            ReturnUrl = returnUrl ?? TempData.Peek("2fa_return_url")?.ToString();
            return Page();
        }

        var rememberMeRaw = TempData.Peek("2fa_remember_me")?.ToString();
        var rememberMe = bool.TryParse(rememberMeRaw, out var remember) && remember;
        var finalReturnUrl = returnUrl ?? TempData.Peek("2fa_return_url")?.ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new("tenant_id", user.TenantId.ToString())
        };

        if (user.Roles != null)
        {
            foreach (var role in user.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.Name));
            }
        }

        user.RecordSuccessfulLogin();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        user.LastLoginIp = ipAddress;
        await _dbContext.SaveChangesAsync();

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe
            });

        try
        {
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
            var deviceInfo = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
                ? "Mobile Device" : "Desktop/Laptop";
            var browser = ParseBrowser(userAgent);
            var tokenHash = Guid.NewGuid().ToString("N");

            var session = await _sessionService.CreateSessionAsync(
                user.Id, user.TenantId, tokenHash,
                deviceInfo, ipAddress, userAgent);

            if (string.IsNullOrEmpty(session.Browser))
            {
                session.Browser = browser;
                await _dbContext.SaveChangesAsync();
            }

            Response.Cookies.Append("KasahQmsSession", tokenHash, new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(30)
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create session record for user {UserId}", user.Id);
        }

        await _auditLoggingService.LogUserLoginAsync(user.Id);

        TempData.Remove("2fa_user_id");
        TempData.Remove("2fa_remember_me");
        TempData.Remove("2fa_return_url");

        if (user.RequirePasswordChange)
        {
            return RedirectToPage("/Account/ChangePassword");
        }

        var dashboardRoute = _dashboardRoutingService.GetDashboardRouteForUser(user);
        return LocalRedirect(finalReturnUrl ?? dashboardRoute);
    }

    private static string ParseBrowser(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return "Unknown";
        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase)) return "Opera";
        if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase)) return "Safari";
        return "Unknown";
    }
}
