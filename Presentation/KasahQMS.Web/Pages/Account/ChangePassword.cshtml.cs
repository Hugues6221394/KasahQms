using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Identity.Commands;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Account;

public class ChangePasswordModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly DashboardRoutingService _dashboardRoutingService;

    public ChangePasswordModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMediator mediator,
        DashboardRoutingService dashboardRoutingService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _dashboardRoutingService = dashboardRoutingService;
    }

    [BindProperty] public string CurrentPassword { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

    public bool IsFirstLogin { get; set; }

    public async Task OnGetAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
            IsFirstLogin = user?.RequirePasswordChange ?? false;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            return RedirectToPage("/Account/Login");
        }

        if (NewPassword != ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Passwords do not match.");
            await OnGetAsync();
            return Page();
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        
        if (user == null)
        {
            return RedirectToPage("/Account/Login");
        }

        IsFirstLogin = user.RequirePasswordChange;

        // For first login, skip current password verification
        var command = new ChangePasswordCommand(
            CurrentPassword,
            NewPassword,
            IsFirstLogin);

        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to change password.");
            await OnGetAsync();
            return Page();
        }

        // Use role-based dashboard routing after password change
        var dashboardRoute = await _dashboardRoutingService.GetDashboardRouteAsync();
        return Redirect(dashboardRoute);
    }
}
