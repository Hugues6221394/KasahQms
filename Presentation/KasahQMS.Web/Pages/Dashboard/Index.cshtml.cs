using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Dashboard;

/// <summary>
/// Generic dashboard index - redirects to role-specific dashboard.
/// </summary>
public class IndexModel : PageModel
{
    private readonly DashboardRoutingService _dashboardRoutingService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        DashboardRoutingService dashboardRoutingService,
        ILogger<IndexModel> logger)
    {
        _dashboardRoutingService = dashboardRoutingService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Redirect to role-specific dashboard
        var dashboardRoute = await _dashboardRoutingService.GetDashboardRouteAsync();
        _logger.LogInformation("Redirecting user to role-specific dashboard: {Route}", dashboardRoute);
        return Redirect(dashboardRoute);
    }
}


