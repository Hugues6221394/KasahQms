using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.AuditLogs.Queries;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.AuditLogs;

/// <summary>
/// Audit Log viewer page. Accessible by Auditors and Admins.
/// Per QMS requirements, Auditors have read-only access to:
/// - Documents (all versions)
/// - Approval history
/// - Audit logs
/// And can export as PDF/ZIP.
/// </summary>
[Authorize(Roles = "Auditor,System Admin,SystemAdmin,Admin,TenantAdmin,TMD,TopManagingDirector")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        IMediator mediator,
        ICurrentUserService currentUserService,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? DepartmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DocumentType { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }

    public List<AuditLogRow> Logs { get; set; } = new();
    public List<OrganizationUnitOption> Departments { get; set; } = new();
    public List<string> EntityTypes { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId ?? 
            await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (tenantId == Guid.Empty) return;

        // Load filter options
        Departments = await _dbContext.OrganizationUnits
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationUnitOption(o.Id, o.Name))
            .ToListAsync();

        EntityTypes = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == tenantId)
            .Select(a => a.EntityType)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync();

        var query = _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId);

        if (StartDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= StartDate.Value);
        }

        if (EndDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= EndDate.Value.AddDays(1));
        }

        if (DepartmentId.HasValue)
        {
            var userIds = await _dbContext.Users
                .Where(u => u.OrganizationUnitId == DepartmentId.Value)
                .Select(u => u.Id)
                .ToListAsync();
            query = query.Where(a => a.UserId.HasValue && userIds.Contains(a.UserId.Value));
        }

        if (!string.IsNullOrWhiteSpace(DocumentType))
        {
            query = query.Where(a => a.EntityType == DocumentType);
        }

        // Get total count for pagination
        TotalItems = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalItems / (double)PageSize);
        PageNumber = Math.Max(1, Math.Min(PageNumber, TotalPages > 0 ? TotalPages : 1));

        Logs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .GroupJoin(_dbContext.Users.AsNoTracking(),
                log => log.UserId,
                user => user.Id,
                (log, users) => new { log, users })
            .SelectMany(x => x.users.DefaultIfEmpty(),
                (x, user) => new AuditLogRow(
                    x.log.Id,
                    x.log.Timestamp,
                    x.log.Timestamp.ToString("MMM dd, yyyy HH:mm:ss"),
                    user != null ? user.FullName : "System",
                    x.log.Action,
                    x.log.EntityType,
                    x.log.EntityId,
                    x.log.IsSuccessful,
                    x.log.IsSuccessful ? "Success" : "Failed",
                    x.log.Description,
                    x.log.IpAddress))
            .ToListAsync();

        _logger.LogInformation("Audit logs accessed by {UserId}. Filters: StartDate={StartDate}, EndDate={EndDate}, Dept={DeptId}, Type={Type}. Showing {Count} of {Total} entries.", 
            _currentUserService.UserId, StartDate, EndDate, DepartmentId, DocumentType, Logs.Count, TotalItems);
    }

    public async Task<IActionResult> OnPostExportAsync(string format = "PDF")
    {
        _logger.LogInformation("Audit log export requested by {UserId} in {Format} format", 
            _currentUserService.UserId, format);

        var query = new ExportAuditLogsQuery(StartDate, EndDate, DepartmentId, DocumentType, format);
        var result = await _mediator.Send(query);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Audit log export failed: {Error}", result.ErrorMessage);
            TempData["Error"] = result.ErrorMessage ?? "Export failed.";
            return RedirectToPage();
        }

        var contentType = format.ToUpper() == "PDF" ? "text/plain" : "application/json";
        var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.{(format.ToUpper() == "PDF" ? "txt" : "json")}";

        _logger.LogInformation("Audit log exported successfully by {UserId}. File: {FileName}", 
            _currentUserService.UserId, fileName);

        return File(result.Value, contentType, fileName);
    }

    public record AuditLogRow(
        Guid Id,
        DateTime TimestampValue,
        string Timestamp, 
        string Actor, 
        string Action, 
        string Entity, 
        Guid? EntityId,
        bool IsSuccessful,
        string Status, 
        string? Description,
        string? IpAddress);
    public record OrganizationUnitOption(Guid Id, string Name);
}


