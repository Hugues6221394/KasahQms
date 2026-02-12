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

    public LoginModel(
        ILogger<LoginModel> logger,
        ApplicationDbContext dbContext,
        IPasswordHasher passwordHasher,
        DashboardRoutingService dashboardRoutingService,
        IAuditLoggingService auditLoggingService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _dashboardRoutingService = dashboardRoutingService;
        _auditLoggingService = auditLoggingService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

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
            user.LastLoginIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _dbContext.SaveChangesAsync();

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

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
