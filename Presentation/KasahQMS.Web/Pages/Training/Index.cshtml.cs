using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowArchived { get; set; }

    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int ExpiredCount { get; set; }
    public int ArchivedCount { get; set; }
    public bool CanCreateTraining { get; set; }

    public List<TrainingRow> Records { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId;

        if (currentUserId == null)
        {
            Records = new();
            return;
        }

        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
        {
            Records = new();
            return;
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();

        // Check role level
        var isTmdOrDeputy = roles.Any(r =>
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase));

        var isManager = !isTmdOrDeputy && roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase));

        var query = _dbContext.TrainingRecords.AsNoTracking()
            .Include(t => t.User)
            .Where(t => t.TenantId == tenantId);

        // Staff: can only see their own trainings
        // Manager: can see their subordinates' trainings + their own
        // TMD/Deputy: can see all trainings
        if (!isTmdOrDeputy && !isManager)
        {
            // Staff - records where they are trainee or assigned trainer
            query = query.Where(t => t.UserId == currentUserId || t.TrainerId == currentUserId);
        }
        else if (isManager && !isTmdOrDeputy)
        {
            // Manager - subordinates + self (as trainee) + records where manager is assigned trainer
            var subordinateIds = await _dbContext.Users
                .Where(u => u.ManagerId == currentUserId && u.IsActive)
                .Select(u => u.Id)
                .ToListAsync();
            
            subordinateIds.Add(currentUserId.Value);
            query = query.Where(t => subordinateIds.Contains(t.UserId) || t.TrainerId == currentUserId);
        }
        // TMD/Deputy: no filter (see all)

        if (!string.IsNullOrWhiteSpace(StatusFilter) && Enum.TryParse<TrainingStatus>(StatusFilter, out var status))
            query = query.Where(t => t.Status == status);

        if (!string.IsNullOrWhiteSpace(TypeFilter) && Enum.TryParse<TrainingType>(TypeFilter, out var type))
            query = query.Where(t => t.TrainingType == type);

        if (DateFrom.HasValue)
            query = query.Where(t => t.ScheduledDate >= DateFrom.Value);

        if (DateTo.HasValue)
            query = query.Where(t => t.ScheduledDate <= DateTo.Value);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.Trim();
            query = query.Where(t =>
                t.Title.Contains(term) ||
                (t.User != null && ((t.User.FirstName + " " + t.User.LastName).Contains(term))));
        }

        var recordsData = await query
            .OrderByDescending(t => t.ScheduledDate)
            .Select(t => new
            {
                t.Id,
                t.Title,
                Employee = t.User != null ? t.User.FirstName + " " + t.User.LastName : "—",
                Type = t.TrainingType,
                Status = t.Status,
                t.ScheduledDate,
                t.CompletedDate,
                t.Score,
                t.Notes
            })
            .ToListAsync();

        var projected = recordsData
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Employee,
                t.Type,
                t.Status,
                t.ScheduledDate,
                t.CompletedDate,
                t.Score,
                IsArchived = IsArchivedFromNotes(t.Notes)
            })
            .ToList();

        ArchivedCount = projected.Count(t => t.IsArchived);
        var visibleRecords = ShowArchived
            ? projected.Where(t => t.IsArchived).ToList()
            : projected.Where(t => !t.IsArchived).ToList();

        TotalCount = visibleRecords.Count;
        CompletedCount = visibleRecords.Count(t => t.Status == TrainingStatus.Completed);
        InProgressCount = visibleRecords.Count(t => t.Status == TrainingStatus.InProgress);
        ExpiredCount = visibleRecords.Count(t => t.Status == TrainingStatus.Expired);

        Records = visibleRecords
            .Select(t => new TrainingRow(
                t.Id,
                t.Title,
                t.Employee,
                t.Type.ToString(),
                GetTypeBadgeClass(t.Type),
                t.Status.ToString(),
                GetStatusBadgeClass(t.Status),
                t.ScheduledDate.ToString("MMM dd, yyyy"),
                t.CompletedDate.HasValue ? t.CompletedDate.Value.ToString("MMM dd, yyyy") : "—",
                t.Score,
                t.IsArchived))
            .ToList();

        // Set CanCreateTraining flag
        CanCreateTraining = isTmdOrDeputy || (isManager && await _dbContext.Users.AnyAsync(u => u.ManagerId == currentUserId && u.IsActive));

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

    private static bool IsArchivedFromNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return false;
        try
        {
            using var doc = JsonDocument.Parse(notes);
            return doc.RootElement.TryGetProperty("IsArchived", out var archived) &&
                   archived.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

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
        int? Score,
        bool IsArchived);
}
