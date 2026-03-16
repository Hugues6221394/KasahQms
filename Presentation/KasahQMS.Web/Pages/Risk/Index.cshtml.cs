using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Risk;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CategoryFilter { get; set; }

    public int TotalCount { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }

    public List<RiskRow> Risks { get; set; } = new();

    // Heat map: [likelihood-1, impact-1] = count
    public int[,] HeatMap { get; set; } = new int[5, 5];

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var query = _dbContext.RiskAssessments.AsNoTracking()
            .Include(r => r.Owner)
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(StatusFilter) && Enum.TryParse<RiskStatus>(StatusFilter, out var status))
            query = query.Where(r => r.Status == status);

        if (!string.IsNullOrWhiteSpace(CategoryFilter))
            query = query.Where(r => r.Category == CategoryFilter);

        var allRisks = await query.ToListAsync();

        TotalCount = allRisks.Count;
        CriticalCount = allRisks.Count(r => r.RiskScore >= 20);
        HighCount = allRisks.Count(r => r.RiskScore >= 15 && r.RiskScore < 20);
        MediumCount = allRisks.Count(r => r.RiskScore >= 8 && r.RiskScore < 15);
        LowCount = allRisks.Count(r => r.RiskScore < 8);

        // Build heat map
        foreach (var r in allRisks)
        {
            var li = Math.Clamp(r.Likelihood - 1, 0, 4);
            var ii = Math.Clamp(r.Impact - 1, 0, 4);
            HeatMap[li, ii]++;
        }

        Risks = allRisks
            .OrderByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.CreatedAt)
            .Select(r => new RiskRow(
                r.Id,
                r.RiskNumber,
                r.Title,
                r.Category ?? "—",
                r.Likelihood,
                r.Impact,
                r.RiskScore,
                GetScoreBadgeClass(r.RiskScore),
                r.Status.ToString(),
                GetStatusBadgeClass(r.Status),
                r.Owner != null ? r.Owner.FirstName + " " + r.Owner.LastName : "—"))
            .ToList();

        _logger.LogInformation("Risk index accessed by {UserId}. Showing {Count} records.",
            _currentUserService.UserId, Risks.Count);
    }

    public static string GetScoreBadgeClass(int score) => score switch
    {
        >= 20 => "bg-rose-100 text-rose-700",
        >= 15 => "bg-orange-100 text-orange-700",
        >= 8 => "bg-amber-100 text-amber-700",
        _ => "bg-emerald-100 text-emerald-700"
    };

    public static string GetScoreLabel(int score) => score switch
    {
        >= 20 => "Critical",
        >= 15 => "High",
        >= 8 => "Medium",
        _ => "Low"
    };

    private static string GetStatusBadgeClass(RiskStatus s) => s switch
    {
        RiskStatus.Mitigated => "bg-emerald-100 text-emerald-700",
        RiskStatus.Closed => "bg-slate-100 text-slate-600",
        RiskStatus.Accepted => "bg-sky-100 text-sky-700",
        RiskStatus.Assessed => "bg-brand-100 text-brand-700",
        _ => "bg-amber-100 text-amber-700"
    };

    public static string GetHeatCellClass(int likelihood, int impact)
    {
        var score = likelihood * impact;
        return score switch
        {
            >= 20 => "bg-rose-500 text-white",
            >= 15 => "bg-orange-400 text-white",
            >= 8 => "bg-amber-300 text-amber-900",
            _ => "bg-emerald-200 text-emerald-800"
        };
    }

    public record RiskRow(
        Guid Id, string RiskNumber, string Title, string Category,
        int Likelihood, int Impact, int Score, string ScoreClass,
        string Status, string StatusClass, string Owner);
}
