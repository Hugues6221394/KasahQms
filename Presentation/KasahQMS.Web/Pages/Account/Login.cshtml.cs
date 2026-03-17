using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly ILogger<LoginModel> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly DashboardRoutingService _dashboardRoutingService;
    private readonly IAuditLoggingService _auditLoggingService;
    private readonly ISessionService _sessionService;

    public LoginModel(
        ILogger<LoginModel> logger,
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        DashboardRoutingService dashboardRoutingService,
        IAuditLoggingService auditLoggingService,
        ISessionService sessionService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _dashboardRoutingService = dashboardRoutingService;
        _auditLoggingService = auditLoggingService;
        _sessionService = sessionService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ReturnUrl { get; set; }

    public List<SampleCredential> SampleCredentials { get; set; } = new();
    public string? PasswordHint { get; set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        await LoadSampleCredentialsAsync();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        await LoadSampleCredentialsAsync();
        ModelState.Remove(nameof(RememberMe)); // prevent "on" parse error on re-render

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            _logger.LogInformation("Login attempt for user {Email}", Email);

            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Please enter your email and password.";
                return Page();
            }

            var user = await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == Email.ToLower());

            if (user == null || !_passwordHasher.Verify(Password, user.PasswordHash))
            {
                await _auditLoggingService.LogFailedLoginAsync(Email);
                ErrorMessage = "Invalid email or password.";
                return Page();
            }

            if (!user.IsActive || user.IsLockedOut)
            {
                await _auditLoggingService.LogActionAsync("LOGIN_AUTH_DISABLED", "User", user.Id, "Account inactive or locked", false);
                ErrorMessage = "Your account is inactive or locked. Contact your system administrator.";
                return Page();
            }

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

            // Record successful login with IP
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
                    IsPersistent = RememberMe,
                    ExpiresUtc = RememberMe
                        ? DateTimeOffset.UtcNow.AddDays(30)
                        : DateTimeOffset.UtcNow.AddHours(8)
                });

            // Create session record with device/browser info
            try
            {
                var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
                var deviceInfo = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase)
                    ? "Mobile Device" : "Desktop/Laptop";
                var browser = ParseBrowser(userAgent);
                var tokenHash = Guid.NewGuid().ToString("N"); // simple token for session tracking

                var session = await _sessionService.CreateSessionAsync(
                    user.Id, user.TenantId, tokenHash,
                    deviceInfo, ipAddress, userAgent);

                // Parse and set browser if not already set
                if (string.IsNullOrEmpty(session.Browser))
                {
                    session.Browser = browser;
                    await _dbContext.SaveChangesAsync();
                }

                // Store session token in separate cookie for current-session detection
                Response.Cookies.Append("KasahQmsSession", tokenHash, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create session record for user {UserId}", user.Id);
            }

            await _auditLoggingService.LogUserLoginAsync(user.Id);

            if (user.RequirePasswordChange)
            {
                return RedirectToPage("/Account/ChangePassword");
            }

            // Use role-based dashboard routing (user from login; HttpContext not yet updated same request)
            var dashboardRoute = _dashboardRoutingService.GetDashboardRouteForUser(user);
            return LocalRedirect(returnUrl ?? dashboardRoute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Email}", Email);
            ErrorMessage = "Invalid email or password.";
            return Page();
        }
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

    private async Task LoadSampleCredentialsAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        if (tenantId == Guid.Empty)
        {
            SampleCredentials = new List<SampleCredential>();
            return;
        }

        PasswordHint = await _dbContext.SystemSettings
            .Where(s => s.TenantId == tenantId && s.Key == "Seed.PasswordHint")
            .Select(s => s.Value)
            .FirstOrDefaultAsync() ?? "P@ssw0rd!";

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Include(u => u.Roles)
            .OrderBy(u => u.FirstName)
            .ToListAsync();

                SampleCredentials = users
            .Select(u => new SampleCredential(
                u.Roles?.FirstOrDefault()?.Name ?? "User",
                u.Email,
                PasswordHint ?? "P@ssw0rd!"))
            .ToList();
    }
}

public record SampleCredential(string Role, string Email, string Password);


