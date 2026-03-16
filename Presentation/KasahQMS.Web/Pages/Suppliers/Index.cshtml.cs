using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Suppliers;

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
    public int QualifiedCount { get; set; }
    public int ConditionallyCount { get; set; }
    public int PendingCount { get; set; }
    public int DisqualifiedCount { get; set; }

    public List<SupplierRow> Suppliers { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var query = _dbContext.Suppliers.AsNoTracking()
            .Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(StatusFilter) && Enum.TryParse<SupplierQualificationStatus>(StatusFilter, out var status))
            query = query.Where(s => s.QualificationStatus == status);

        if (!string.IsNullOrWhiteSpace(CategoryFilter))
            query = query.Where(s => s.Category == CategoryFilter);

        TotalCount = await query.CountAsync();
        QualifiedCount = await query.CountAsync(s => s.QualificationStatus == SupplierQualificationStatus.Qualified);
        ConditionallyCount = await query.CountAsync(s => s.QualificationStatus == SupplierQualificationStatus.Conditionally);
        PendingCount = await query.CountAsync(s => s.QualificationStatus == SupplierQualificationStatus.Pending);
        DisqualifiedCount = await query.CountAsync(s => s.QualificationStatus == SupplierQualificationStatus.Disqualified);

        Suppliers = await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierRow(
                s.Id,
                s.Code,
                s.Name,
                s.Category ?? "—",
                s.ContactName ?? "—",
                s.QualificationStatus.ToString(),
                GetStatusBadgeClass(s.QualificationStatus),
                s.PerformanceScore,
                s.NextAuditDate.HasValue ? s.NextAuditDate.Value.ToString("MMM dd, yyyy") : "—"))
            .ToListAsync();

        _logger.LogInformation("Suppliers index accessed by {UserId}. Showing {Count} records.",
            _currentUserService.UserId, Suppliers.Count);
    }

    public static string GetStatusBadgeClass(SupplierQualificationStatus s) => s switch
    {
        SupplierQualificationStatus.Qualified => "bg-emerald-100 text-emerald-700",
        SupplierQualificationStatus.Conditionally => "bg-amber-100 text-amber-700",
        SupplierQualificationStatus.Pending => "bg-brand-100 text-brand-700",
        SupplierQualificationStatus.Suspended => "bg-orange-100 text-orange-700",
        SupplierQualificationStatus.Disqualified => "bg-rose-100 text-rose-700",
        _ => "bg-slate-100 text-slate-600"
    };

    public record SupplierRow(
        Guid Id, string Code, string Name, string Category, string Contact,
        string Status, string StatusClass, decimal? PerformanceScore, string NextAuditDate);
}
