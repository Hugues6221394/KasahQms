using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Audits;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Type { get; set; }

    public int PlannedCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int OpenFindingsCount { get; set; }
    
    /// <summary>
    /// Only TMD and Deputy can schedule/create audits.
    /// Auditors execute audits, they do not schedule them.
    /// </summary>
    public bool CanScheduleAudit { get; set; }
    
    /// <summary>
    /// True if current user is an auditor (view-only for scheduling).
    /// </summary>
    public bool IsAuditor { get; set; }

    public List<AuditRow> Audits { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        
        // Check user roles for scheduling permission
        var userId = _currentUserService.UserId;
        if (userId.HasValue)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == userId.Value);
            
            if (user?.Roles != null)
            {
                var roleNames = user.Roles.Select(r => r.Name).ToList();
                
                // Auditors cannot schedule audits - they only execute them
                IsAuditor = roleNames.Any(r => 
                    r.Equals("Auditor", StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("Internal Auditor", StringComparison.OrdinalIgnoreCase));
                
                // Only TMD and Deputy can schedule audits
                CanScheduleAudit = !IsAuditor && roleNames.Any(r =>
                    r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase) ||
                    r.Contains("Operations", StringComparison.OrdinalIgnoreCase));
            }
        }
        var query = _dbContext.Audits.AsNoTracking()
            .Include(a => a.LeadAuditor)
            .Where(a => a.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(a => a.Title.Contains(SearchTerm) || a.AuditNumber.Contains(SearchTerm));
        }

        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<AuditStatus>(Status, out var status))
        {
            query = query.Where(a => a.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(Type) && Enum.TryParse<AuditType>(Type, out var auditType))
        {
            query = query.Where(a => a.AuditType == auditType);
        }

        PlannedCount = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && a.Status == AuditStatus.Planned);
        InProgressCount = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && a.Status == AuditStatus.InProgress);
        CompletedCount = await _dbContext.Audits.CountAsync(a => a.TenantId == tenantId && a.Status == AuditStatus.Completed);
        OpenFindingsCount = await _dbContext.AuditFindings
            .Join(_dbContext.Audits.Where(a => a.TenantId == tenantId),
                f => f.AuditId,
                a => a.Id,
                (f, _) => f)
            .CountAsync(f => f.Status == "Open");

        var findingCounts = await _dbContext.AuditFindings.AsNoTracking()
            .Join(_dbContext.Audits.AsNoTracking().Where(a => a.TenantId == tenantId),
                f => f.AuditId,
                a => a.Id,
                (f, _) => f)
            .GroupBy(f => f.AuditId)
            .Select(g => new
            {
                AuditId = g.Key,
                Total = g.Count(),
                Major = g.Count(f => f.Severity == FindingSeverity.Major || f.Severity == FindingSeverity.Critical)
            })
            .ToListAsync();

        var auditsList = await query
            .OrderByDescending(a => a.PlannedStartDate)
            .ToListAsync();

        Audits = auditsList
            .Select(a => new AuditRow(
                a.Id,
                a.Title,
                a.AuditNumber,
                a.AuditType.ToString(),
                GetStatusClass(a.Status),
                a.Status.ToString(),
                a.LeadAuditor != null ? a.LeadAuditor.FullName : "Unassigned",
                $"{a.PlannedStartDate:MMM dd} - {a.PlannedEndDate:MMM dd, yyyy}",
                findingCounts.FirstOrDefault(f => f.AuditId == a.Id)?.Total ?? 0,
                findingCounts.FirstOrDefault(f => f.AuditId == a.Id)?.Major ?? 0))
            .ToList();

        _logger.LogInformation("Audits page accessed with filters: Search={Search}, Status={Status}, Type={Type}",
            SearchTerm, Status, Type);
    }

    private static string GetStatusClass(AuditStatus status)
    {
        return status switch
        {
            AuditStatus.Completed => "bg-emerald-100 text-emerald-700",
            AuditStatus.InProgress => "bg-amber-100 text-amber-700",
            AuditStatus.Closed => "bg-slate-100 text-slate-600",
            _ => "bg-brand-100 text-brand-700"
        };
    }

    public record AuditRow(
        Guid Id,
        string Title,
        string Number,
        string Type,
        string StatusClass,
        string Status,
        string LeadAuditor,
        string DateRange,
        int Findings,
        int MajorFindings);
}

