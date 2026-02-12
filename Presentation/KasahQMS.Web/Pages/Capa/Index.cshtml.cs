using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Capa;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ApplicationDbContext _dbContext;

    public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Priority { get; set; }

    public int DraftCount { get; set; }
    public int UnderInvestigationCount { get; set; }
    public int ActionsDefinedCount { get; set; }
    public int ActionsImplementedCount { get; set; }
    public int VerifiedCount { get; set; }
    public int ClosedCount { get; set; }

    public List<CapaRow> Capas { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var query = _dbContext.Capas.AsNoTracking()
            .Include(c => c.Owner)
            .Where(c => c.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(c => c.Title.Contains(SearchTerm) || c.CapaNumber.Contains(SearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<CapaStatus>(Status, out var status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(Priority) && Enum.TryParse<CapaPriority>(Priority, out var priority))
        {
            query = query.Where(c => c.Priority == priority);
        }

        DraftCount = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && c.Status == CapaStatus.Draft);
        UnderInvestigationCount = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && c.Status == CapaStatus.UnderInvestigation);
        ActionsDefinedCount = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && c.Status == CapaStatus.ActionsDefined);
        ActionsImplementedCount = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && c.Status == CapaStatus.ActionsImplemented);
        VerifiedCount = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && c.Status == CapaStatus.EffectivenessVerified);
        ClosedCount = await _dbContext.Capas.CountAsync(c => c.TenantId == tenantId && c.Status == CapaStatus.Closed);

        Capas = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CapaRow(
                c.Id,
                c.CapaNumber,
                c.Title,
                c.CapaType.ToString(),
                c.Priority.ToString(),
                GetPriorityClass(c.Priority),
                c.Status.ToString(),
                GetStatusClass(c.Status),
                c.Owner != null ? c.Owner.FullName : "Unassigned",
                c.TargetCompletionDate.HasValue ? c.TargetCompletionDate.Value.ToString("MMM dd, yyyy") : "No due date"))
            .ToListAsync();

        _logger.LogInformation("CAPA page accessed with filters: Search={Search}, Status={Status}, Priority={Priority}",
            SearchTerm, Status, Priority);
    }

    private static string GetStatusClass(CapaStatus status)
    {
        return status switch
        {
            CapaStatus.Closed => "bg-green-100 text-green-800",
            CapaStatus.EffectivenessVerified => "bg-emerald-100 text-emerald-700",
            CapaStatus.ActionsImplemented => "bg-indigo-100 text-indigo-700",
            CapaStatus.ActionsDefined => "bg-blue-100 text-blue-700",
            CapaStatus.UnderInvestigation => "bg-amber-100 text-amber-700",
            CapaStatus.Draft => "bg-slate-100 text-slate-700",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    private static string GetPriorityClass(CapaPriority priority)
    {
        return priority switch
        {
            CapaPriority.Critical => "bg-rose-100 text-rose-700",
            CapaPriority.High => "bg-amber-100 text-amber-700",
            CapaPriority.Medium => "bg-brand-100 text-brand-700",
            CapaPriority.Low => "bg-emerald-100 text-emerald-700",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    public record CapaRow(
        Guid Id,
        string Number,
        string Title,
        string Type,
        string Priority,
        string PriorityClass,
        string Status,
        string StatusClass,
        string Owner,
        string DueDate);
}
