using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Supplier;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Suppliers;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public SupplierDetailView? Supplier { get; set; }
    public List<AuditRow> AuditHistory { get; set; } = new();
    public List<UserOption> Users { get; set; } = new();
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }

    [BindProperty] public DateTime AuditDate { get; set; } = DateTime.UtcNow.Date.AddDays(30);
    [BindProperty] public Guid AuditorId { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        return await LoadSupplierAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostQualifyAsync(Guid id)
    {
        return await TransitionStatus(id, SupplierQualificationStatus.Qualified, "Supplier qualified.");
    }

    public async Task<IActionResult> OnPostConditionalAsync(Guid id)
    {
        return await TransitionStatus(id, SupplierQualificationStatus.Conditionally, "Supplier conditionally approved.");
    }

    public async Task<IActionResult> OnPostSuspendAsync(Guid id)
    {
        return await TransitionStatus(id, SupplierQualificationStatus.Suspended, "Supplier suspended.");
    }

    public async Task<IActionResult> OnPostDisqualifyAsync(Guid id)
    {
        return await TransitionStatus(id, SupplierQualificationStatus.Disqualified, "Supplier disqualified.");
    }

    public async Task<IActionResult> OnPostScheduleAuditAsync(Guid id)
    {
        if (AuditorId == Guid.Empty)
            return RedirectToPage(new { id, message = "Auditor is required.", success = false });

        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var audit = new SupplierAudit
        {
            Id = Guid.NewGuid(),
            SupplierId = id,
            TenantId = tenantId,
            AuditDate = AuditDate,
            AuditorId = AuditorId,
            Score = 0,
            Status = SupplierAuditStatus.Scheduled
        };

        _dbContext.SupplierAudits.Add(audit);

        // Update next audit date on supplier
        var supplier = await GetSupplierEntity(id);
        if (supplier != null)
        {
            supplier.NextAuditDate = AuditDate;
            supplier.LastModifiedById = _currentUserService.UserId;
            supplier.LastModifiedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Supplier audit scheduled for {SupplierId} by {UserId}", id, _currentUserService.UserId);
        return RedirectToPage(new { id, message = "Audit scheduled.", success = true });
    }

    private async Task<IActionResult> TransitionStatus(Guid id, SupplierQualificationStatus newStatus, string msg)
    {
        var supplier = await GetSupplierEntity(id);
        if (supplier == null) return NotFound();

        supplier.QualificationStatus = newStatus;
        if (newStatus == SupplierQualificationStatus.Qualified)
            supplier.QualifiedDate = DateTime.UtcNow;
        supplier.LastModifiedById = _currentUserService.UserId;
        supplier.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { id, message = msg, success = true });
    }

    private async Task<Domain.Entities.Supplier.Supplier?> GetSupplierEntity(Guid id)
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        return await _dbContext.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenantId);
    }

    private async Task<bool> LoadSupplierAsync(Guid id)
    {
        var tenantId = _currentUserService.TenantId
            ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        var s = await _dbContext.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);

        if (s == null) return false;

        Supplier = new SupplierDetailView(
            s.Id, s.Code, s.Name, s.Category ?? "—",
            s.ContactName, s.ContactEmail, s.ContactPhone, s.Address,
            s.QualificationStatus.ToString(),
            IndexModel.GetStatusBadgeClass(s.QualificationStatus),
            s.QualifiedDate?.ToString("MMM dd, yyyy"),
            s.NextAuditDate?.ToString("MMM dd, yyyy"),
            s.PerformanceScore,
            s.Notes,
            s.CreatedAt.ToString("MMM dd, yyyy HH:mm"));

        AuditHistory = await _dbContext.SupplierAudits.AsNoTracking()
            .Include(a => a.Auditor)
            .Where(a => a.SupplierId == id && a.TenantId == tenantId)
            .OrderByDescending(a => a.AuditDate)
            .Select(a => new AuditRow(
                a.Id,
                a.AuditDate.ToString("MMM dd, yyyy"),
                a.Auditor != null ? a.Auditor.FirstName + " " + a.Auditor.LastName : "—",
                a.Score,
                a.Status.ToString(),
                a.Findings,
                a.CorrectiveActionsRequired,
                a.CompletedAt.HasValue ? a.CompletedAt.Value.ToString("MMM dd, yyyy") : null))
            .ToListAsync();

        Users = await _dbContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new UserOption(u.Id, u.FirstName + " " + u.LastName))
            .ToListAsync();

        return true;
    }

    public record SupplierDetailView(
        Guid Id, string Code, string Name, string Category,
        string? ContactName, string? ContactEmail, string? ContactPhone, string? Address,
        string Status, string StatusClass, string? QualifiedDate, string? NextAuditDate,
        decimal? PerformanceScore, string? Notes, string CreatedAt);

    public record AuditRow(
        Guid Id, string AuditDate, string Auditor, decimal Score, string Status,
        string? Findings, string? CorrectiveActions, string? CompletedDate);

    public record UserOption(Guid Id, string Name);
}
