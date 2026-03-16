using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Training;

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
    public string? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? DateTo { get; set; }

    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int ExpiredCount { get; set; }

    public List<TrainingRow> Records { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var query = _dbContext.TrainingRecords.AsNoTracking()
            .Include(t => t.User)
            .Where(t => t.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(StatusFilter) && Enum.TryParse<TrainingStatus>(StatusFilter, out var status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(TypeFilter) && Enum.TryParse<TrainingType>(TypeFilter, out var type))
            query = query.Where(t => t.TrainingType == type);

        if (DateFrom.HasValue)
            query = query.Where(t => t.ScheduledDate >= DateFrom.Value);

        if (DateTo.HasValue)
            query = query.Where(t => t.ScheduledDate <= DateTo.Value);

        TotalCount = await query.CountAsync();
        CompletedCount = await query.CountAsync(t => t.Status == TrainingStatus.Completed);
        InProgressCount = await query.CountAsync(t => t.Status == TrainingStatus.InProgress);
        ExpiredCount = await query.CountAsync(t => t.Status == TrainingStatus.Expired);

        Records = await query
            .OrderByDescending(t => t.ScheduledDate)
            .Select(t => new TrainingRow(
                t.Id,
                t.Title,
                t.User != null ? t.User.FirstName + " " + t.User.LastName : "—",
                t.TrainingType.ToString(),
                GetTypeBadgeClass(t.TrainingType),
                t.Status.ToString(),
                GetStatusBadgeClass(t.Status),
                t.ScheduledDate.ToString("MMM dd, yyyy"),
                t.CompletedDate.HasValue ? t.CompletedDate.Value.ToString("MMM dd, yyyy") : "—",
                t.Score))
            .ToListAsync();

        _logger.LogInformation("Training index accessed by {UserId}. Showing {Count} records.",
            _currentUserService.UserId, Records.Count);
    }

    private static string GetStatusBadgeClass(TrainingStatus s) => s switch
    {
        TrainingStatus.Completed => "bg-emerald-100 text-emerald-700",
        TrainingStatus.InProgress => "bg-brand-100 text-brand-700",
        TrainingStatus.Expired => "bg-rose-100 text-rose-700",
        _ => "bg-amber-100 text-amber-700"
    };

    private static string GetTypeBadgeClass(TrainingType t) => t switch
    {
        TrainingType.Certification => "bg-purple-100 text-purple-700",
        TrainingType.Refresher => "bg-sky-100 text-sky-700",
        TrainingType.OnTheJob => "bg-teal-100 text-teal-700",
        _ => "bg-slate-100 text-slate-600"
    };

    public record TrainingRow(
        Guid Id,
        string Title,
        string Employee,
        string Type,
        string TypeClass,
        string Status,
        string StatusClass,
        string ScheduledDate,
        string CompletionDate,
        int? Score);
}
