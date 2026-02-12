using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace KasahQMS.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly DashboardRoutingService _dashboardRoutingService;

    public HomeController(
        ILogger<HomeController> logger,
        DashboardRoutingService dashboardRoutingService)
    {
        _logger = logger;
        _dashboardRoutingService = dashboardRoutingService;
    }

    public async Task<IActionResult> Index()
    {
        var dashboardRoute = await _dashboardRoutingService.GetDashboardRouteAsync();
        return Redirect(dashboardRoute);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Route("/Error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}

