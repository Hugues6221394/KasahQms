using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Entities.Audits;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Audits;

[Authorize]
public class ScheduleModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ScheduleModel> _logger;

    public ScheduleModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<ScheduleModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public List<AuditScheduleRow> Audits { get; set; } = new();
    public bool CanManageSchedule { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        var currentUser = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return Unauthorized();

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        
        CanManageSchedule = roles.Any(r => 
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Operations", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("System Admin", StringComparison.OrdinalIgnoreCase));

        var audits = await _dbContext.Audits
            .AsNoTracking()
            .Include(a => a.LeadAuditor)
            .Where(a => a.Status == AuditStatus.Planned || a.Status == AuditStatus.InProgress)
            .OrderBy(a => a.PlannedStartDate)
            .ToListAsync();

        Audits = audits.Select(a => new AuditScheduleRow(
            a.Id,
            a.AuditNumber,
            a.Title,
            a.AuditType.ToString(),
            a.PlannedStartDate,
            a.PlannedEndDate,
            a.LeadAuditor?.FullName ?? "Unassigned",
            a.Status.ToString(),
            GetStatusClass(a.Status)
        )).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        if (userId == null)
            return Unauthorized();

        var currentUser = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (currentUser == null)
            return Unauthorized();

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        
        var canManage = roles.Any(r => 
            r.Contains("TMD", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Top Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Managing Director", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Deputy", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Country Manager", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("Operations", StringComparison.OrdinalIgnoreCase) ||
            r.Contains("System Admin", StringComparison.OrdinalIgnoreCase));

        try
        {
            var audit = await _dbContext.Audits.FindAsync(id);
            if (audit == null)
            {
                ErrorMessage = "Audit not found.";
                await OnGetAsync();
                return Page();
            }

            if (!canManage && audit.CreatedById != userId.Value)
            {
                ErrorMessage = "You do not have permission to delete this audit.";
                await OnGetAsync();
                return Page();
            }

            if (audit.Status != AuditStatus.Planned)
            {
                ErrorMessage = "Only planned audits can be deleted. This audit is " + audit.Status;
                await OnGetAsync();
                return Page();
            }

            _dbContext.Audits.Remove(audit);
            await _dbContext.SaveChangesAsync();

            SuccessMessage = $"Audit '{audit.Title}' deleted successfully.";
            _logger.LogInformation("User {UserId} deleted audit {AuditId}", userId, id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting audit {AuditId}", id);
            ErrorMessage = "An error occurred while deleting the audit.";
        }

        await OnGetAsync();
        return Page();
    }

    private static string GetStatusClass(AuditStatus status)
    {
        return status switch
        {
            AuditStatus.Planned => "bg-brand-100 text-brand-700",
            AuditStatus.InProgress => "bg-amber-100 text-amber-700",
            AuditStatus.Completed => "bg-emerald-100 text-emerald-700",
            AuditStatus.Closed => "bg-slate-100 text-slate-600",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    public record AuditScheduleRow(
        Guid Id,
        string AuditNumber,
        string Title,
        string Type,
        DateTime ScheduledStart,
        DateTime ScheduledEnd,
        string LeadAuditor,
        string Status,
        string StatusClass);
}
