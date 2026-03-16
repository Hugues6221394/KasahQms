using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KasahQMS.Web.Pages.Security;

public class SessionsModel : PageModel
{
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserService _currentUserService;

    public SessionsModel(
        ISessionService sessionService,
        ICurrentUserService currentUserService)
    {
        _sessionService = sessionService;
        _currentUserService = currentUserService;
    }

    public List<SessionRow> Sessions { get; set; } = new();
    public string? CurrentSessionToken { get; set; }
    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadSessionsAsync();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid sessionId)
    {
        try
        {
            await _sessionService.RevokeSessionAsync(sessionId);
            StatusMessage = "Session revoked successfully.";
        }
        catch (Exception)
        {
            ErrorMessage = "Failed to revoke session. Please try again.";
        }

        await LoadSessionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAllAsync()
    {
        var userId = GetUserId();
        if (userId == null) return RedirectToPage();

        try
        {
            await _sessionService.RevokeAllSessionsAsync(userId.Value);
            StatusMessage = "All other sessions have been revoked.";
        }
        catch (Exception)
        {
            ErrorMessage = "Failed to revoke sessions. Please try again.";
        }

        await LoadSessionsAsync();
        return Page();
    }

    private async Task LoadSessionsAsync()
    {
        var userId = GetUserId();
        if (userId == null) return;

        CurrentSessionToken = HttpContext.Request.Cookies["KasahQmsAuth"];

        var sessions = await _sessionService.GetActiveSessionsAsync(userId.Value);
        
        // Parse browser from UserAgent if Browser is null
        foreach (var s in sessions)
        {
            if (string.IsNullOrEmpty(s.Browser) && !string.IsNullOrEmpty(s.UserAgent))
            {
                if (s.UserAgent.Contains("Edg")) s.Browser = "Edge";
                else if (s.UserAgent.Contains("Chrome")) s.Browser = "Chrome";
                else if (s.UserAgent.Contains("Firefox")) s.Browser = "Firefox";
                else if (s.UserAgent.Contains("Safari")) s.Browser = "Safari";
                else s.Browser = "Unknown";
            }
        }
        
        Sessions = sessions.Select(s => new SessionRow(
            s.Id,
            s.DeviceInfo ?? "Unknown Device",
            s.IpAddress ?? "Unknown",
            s.Browser ?? "Unknown",
            s.Location ?? "Unknown",
            s.LastActivityAt,
            s.CreatedAt,
            CurrentSessionToken != null && s.Token == CurrentSessionToken
        )).ToList();
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    public record SessionRow(
        Guid Id,
        string Device,
        string IpAddress,
        string Browser,
        string Location,
        DateTime LastActivity,
        DateTime CreatedAt,
        bool IsCurrent);
}
