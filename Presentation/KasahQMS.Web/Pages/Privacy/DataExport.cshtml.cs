using System.Security.Claims;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Privacy;

public class DataExportModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public DataExportModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<ExportRow> Exports { get; set; } = new();

    public async Task OnGetAsync()
    {
        var userId = GetUserId();
        if (userId == null) return;

        var requests = await _dbContext.DataExportRequests.AsNoTracking()
            .Where(r => r.UserId == userId.Value)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        Exports = requests.Select(r => new ExportRow(
            r.Id,
            r.RequestedAt,
            r.Status.ToString(),
            r.CompletedAt,
            r.DownloadUrl,
            r.ExpiresAt
        )).ToList();
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : null;
    }

    public record ExportRow(
        Guid Id,
        DateTime RequestedAt,
        string Status,
        DateTime? CompletedAt,
        string? DownloadUrl,
        DateTime? ExpiresAt);
}
