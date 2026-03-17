using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AppAuthService = KasahQMS.Application.Common.Security.IAuthorizationService;

namespace KasahQMS.Web.Pages.Employees;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly AppAuthService _authorizationService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        AppAuthService authorizationService,
        ILogger<IndexModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    public List<EmployeeActivityRow> Employees { get; set; } = new();
    public List<EmployeeActivityRow> TopPerformers { get; set; } = new();
    public bool CanView { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null)
            return Unauthorized();

        // Use permission-based check (allows delegation)
        CanView = await _authorizationService.HasPermissionAsync(Application.Common.Security.Permissions.Employees.View);

        if (!CanView)
        {
            return RedirectToPage("/Account/AccessDenied");
        }

        var cutoff = DateTime.UtcNow.AddDays(-7);

        // Get all active users
        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Include(u => u.OrganizationUnit)
            .ToListAsync();

        // Get audit log activity per user in last 7 days
        var activityCounts = await _dbContext.AuditLogEntries
            .Where(a => a.TenantId == tenantId && a.Timestamp > cutoff && a.IsSuccessful)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Get tasks completed per user in last 7 days
        var tasksCompleted = await _dbContext.QmsTasks
            .Where(t => t.TenantId == tenantId 
                && t.Status == QmsTaskStatus.Completed 
                && t.LastModifiedAt > cutoff)
            .GroupBy(t => t.AssignedToId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Get documents created per user in last 7 days
        var documentsCreated = await _dbContext.Documents
            .Where(d => d.TenantId == tenantId && d.CreatedAt > cutoff)
            .GroupBy(d => d.CreatedById)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Build employee activity rows
        var employeeRows = users.Select(u =>
        {
            var activity = activityCounts.FirstOrDefault(a => a.UserId == u.Id)?.Count ?? 0;
            var tasks = tasksCompleted.FirstOrDefault(t => t.UserId == u.Id)?.Count ?? 0;
            var docs = documentsCreated.FirstOrDefault(d => d.UserId == u.Id)?.Count ?? 0;
            var score = (tasks * 2) + docs + activity;

            return new EmployeeActivityRow(
                u.Id,
                u.FullName,
                u.JobTitle ?? "—",
                u.OrganizationUnit?.Name ?? "—",
                tasks,
                docs,
                activity,
                score,
                u.LastLoginAt
            );
        }).OrderByDescending(e => e.ActivityScore).ToList();

        Employees = employeeRows;
        TopPerformers = employeeRows.Take(3).ToList();

        _logger.LogInformation("User {UserId} viewed employee activity report", userId);

        return Page();
    }

    public record EmployeeActivityRow(
        Guid Id,
        string Name,
        string JobTitle,
        string Department,
        int TasksCompleted,
        int DocumentsCreated,
        int AuditEntries,
        int ActivityScore,
        DateTime? LastLogin);
}
