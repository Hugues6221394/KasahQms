using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly IAuditLoggingService _auditLoggingService;
    private readonly ICurrentUserService _currentUserService;

    public LogoutModel(IAuditLoggingService auditLoggingService, ICurrentUserService currentUserService)
    {
        _auditLoggingService = auditLoggingService;
        _currentUserService = currentUserService;
    }

    public async Task OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId.HasValue)
        {
            await _auditLoggingService.LogUserLogoutAsync(userId.Value);
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Redirect("/Account/Login");
    }
}
