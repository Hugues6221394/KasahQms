using System.Text.Json;
using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Documents.Queries;
using KasahQMS.Application.Features.Tasks.Queries;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Dashboard;

/// <summary>
/// Manager dashboard for Department Managers - department-focused view.
/// </summary>
[Authorize(Roles = "DepartmentManager,Department Manager")]
public class ManagerModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IMediator _mediator;
    private readonly ILogger<ManagerModel> _logger;

    public ManagerModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IMediator mediator,
        ILogger<ManagerModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _mediator = mediator;
        _logger = logger;
    }

    public string DisplayName { get; set; } = "Manager";
    public string? DepartmentName { get; set; }
    public List<StatCard> Stats { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public List<ApprovalItem> PendingApprovals { get; set; } = new();
    public List<ActivityItem> Activity { get; set; } = new();
    public List<SubordinateItem> SubordinateWork { get; set; } = new();
    public int DeptDocumentsCount { get; set; }
    public int OpenCapasCount { get; set; }
    public int PendingApprovalsCount { get; set; }
    public int OverdueTasksCount { get; set; }
    public int TeamMemberCount { get; set; }
    public List<TeamMemberItem> TeamMembers { get; set; } = new();
    public int RiskCount { get; set; }
    public string CurrentDate { get; set; } = "";
    public string DocumentsTrendJson { get; set; } = "{}";
    public string TaskStatusJson { get; set; } = "{}";

    public async Task OnGetAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        var tenantId = currentUser?.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (tenantId == Guid.Empty || currentUser == null)
        {
            return;
        }

        DisplayName = currentUser.FullName;
        DepartmentName = currentUser.OrganizationUnit?.Name ?? "Department";
        CurrentDate = DateTime.UtcNow.ToString("dddd, MMMM dd, yyyy");

        // Get visible user IDs (all subordinates recursively)
        var visibleUserIds = await _hierarchyService.GetVisibleUserIdsAsync(currentUser.Id);

        // Department statistics (only for visible users)
        var activeDocuments = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && 
            d.Status != DocumentStatus.Archived &&
            visibleUserIds.Contains(d.CreatedById));
        
        var openCapas = await _dbContext.Capas.CountAsync(c =>
            c.TenantId == tenantId && 
            c.Status != CapaStatus.Closed && 
            c.Status != CapaStatus.EffectivenessVerified &&
            visibleUserIds.Contains(c.CreatedById));
        
        var pendingApprovals = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && 
            (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview) &&
            d.CurrentApproverId == currentUser.Id);
        
        var overdueTasks = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId &&
            t.AssignedToId.HasValue &&
            visibleUserIds.Contains(t.AssignedToId.Value) &&
            t.DueDate < DateTime.UtcNow &&
            t.Status != QmsTaskStatus.Completed &&
            t.Status != QmsTaskStatus.Cancelled);

        Stats = new List<StatCard>
        {
            new("Department Documents", activeDocuments.ToString(), "Created by team", "/Documents", activeDocuments),
            new("Open CAPAs", openCapas.ToString(), "Requires follow-up", "/Capa", openCapas),
            new("Pending Approvals", pendingApprovals.ToString(), "Awaiting your review", "/Documents?status=Submitted", pendingApprovals),
            new("Overdue Tasks", overdueTasks.ToString(), "Team tasks", "/Tasks?status=Overdue", overdueTasks)
        };

        DeptDocumentsCount = activeDocuments;
        OpenCapasCount = openCapas;
        PendingApprovalsCount = pendingApprovals;
        OverdueTasksCount = overdueTasks;

        // Get tasks using refactored query (includes subordinate tasks)
        var myTasksQuery = new GetMyTasksQuery { Limit = 5 };
        var tasksResult = await _mediator.Send(myTasksQuery);
        if (tasksResult.IsSuccess)
        {
            Tasks = tasksResult.Value.UpcomingTasks
                .Take(5)
                .Select(t => new TaskItem(t.Title, t.DueDate?.ToString("MMM dd") ?? "No due date", t.Status.ToString()))
                .ToList();
        }

        // Get pending approvals
        var approvalData = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId &&
                        d.CurrentApproverId == currentUser.Id)
            .Join(_dbContext.Users.AsNoTracking(),
                d => d.CreatedById,
                u => u.Id,
                (d, u) => new { d.Title, u.FirstName, u.LastName, Status = d.Status.ToString() })
            .Take(5)
            .ToListAsync();

        PendingApprovals = approvalData
            .Select(a => new ApprovalItem(a.Title, $"{a.FirstName} {a.LastName}", a.Status))
            .ToList();

        // Recent activity (department only - exclude login/logout)
        Activity = await _dbContext.AuditLogEntries.AsNoTracking()
            .Where(a => a.TenantId == tenantId && 
                       a.UserId.HasValue && 
                       visibleUserIds.Contains(a.UserId.Value) &&
                       a.Action != "USER_LOGIN" && 
                       a.Action != "USER_LOGOUT" &&
                       (a.Action == "DOCUMENT_CREATED" || 
                        a.Action == "DOCUMENT_SUBMITTED" ||
                        a.Action == "TASK_CREATED" ||
                        a.Action == "TASK_COMPLETED" ||
                        a.Action == "CAPA_CREATED" ||
                        a.Action == "AUDIT_SCHEDULED" ||
                        a.EntityType == "Document" ||
                        a.EntityType == "Task" ||
                        a.EntityType == "Capa"))
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .Select(a => new ActivityItem(
                a.Action.Replace("_", " "),
                a.Description ?? a.EntityType,
                a.Timestamp.ToString("MMM dd, HH:mm")))
            .ToListAsync();

        // Subordinate work summary
        var subordinates = await _hierarchyService.GetSubordinateUserIdsAsync(currentUser.Id, recursive: true);
        var subordinateList = subordinates.ToList();
        TeamMemberCount = subordinateList.Count;

        if (subordinateList.Any())
        {
            var memberData = await _dbContext.Users.AsNoTracking()
                .Where(u => subordinateList.Contains(u.Id))
                .Select(u => new
                {
                    u.FullName,
                    u.FirstName,
                    u.LastName,
                    TaskCount = _dbContext.QmsTasks.Count(t => t.AssignedToId == u.Id && t.Status != QmsTaskStatus.Completed && t.Status != QmsTaskStatus.Cancelled),
                    CompletedCount = _dbContext.QmsTasks.Count(t => t.AssignedToId == u.Id && t.Status == QmsTaskStatus.Completed)
                })
                .Take(8)
                .ToListAsync();

            TeamMembers = memberData
                .Select(m => new TeamMemberItem(
                    m.FullName,
                    $"{(m.FirstName?.Length > 0 ? m.FirstName[0].ToString() : "")}{(m.LastName?.Length > 0 ? m.LastName[0].ToString() : "")}",
                    m.TaskCount,
                    m.CompletedCount))
                .ToList();

            var subordinateDocs = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.TenantId == tenantId && 
                           subordinateList.Contains(d.CreatedById) &&
                           (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.Submitted))
                .Join(_dbContext.Users.AsNoTracking(),
                    d => d.CreatedById,
                    u => u.Id,
                    (d, u) => new { d.Title, UserName = u.FullName, Status = d.Status.ToString() })
                .Take(5)
                .ToListAsync();

            SubordinateWork = subordinateDocs
                .Select(d => new SubordinateItem(d.Title, d.UserName, d.Status))
                .ToList();
        }

        try
        {
            RiskCount = await _dbContext.RiskRegisterEntries.AsNoTracking()
                .CountAsync(r => r.Status != "Closed" && r.Status != null);
        }
        catch { RiskCount = 0; }

        DocumentsTrendJson = await BuildDocumentTrendAsync(tenantId, visibleUserIds);
        TaskStatusJson = await BuildTaskStatusAsync(tenantId, visibleUserIds);
    }

    private static string SerializeChart(IEnumerable<string> labels, IEnumerable<int> values)
    {
        return JsonSerializer.Serialize(new
        {
            labels,
            values
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    public record StatCard(string Title, string Value, string Subtitle, string Link, int CountTo);
    public record TaskItem(string Title, string DueDate, string Status);
    public record ApprovalItem(string Title, string Owner, string Stage);
    public record ActivityItem(string Title, string Description, string When);
    public record SubordinateItem(string Title, string Owner, string Status);
    public record TeamMemberItem(string Name, string Initials, int TaskCount, int CompletedCount);

    private async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return await _dbContext.Users
                .Include(u => u.Roles)
                .Include(u => u.OrganizationUnit)
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        }

        return null;
    }

    private async Task<string> BuildDocumentTrendAsync(Guid tenantId, IEnumerable<Guid> visibleUserIds)
    {
        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1).AddMonths(-5), DateTimeKind.Utc);
        var months = Enumerable.Range(0, 6)
            .Select(i => start.AddMonths(i))
            .ToList();

        var userIdList = visibleUserIds.ToList();
        var grouped = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && 
                       d.CreatedAt >= start &&
                       userIdList.Contains(d.CreatedById))
            .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();

        var labels = months.Select(m => m.ToString("MMM")).ToArray();
        var values = months
            .Select(m => grouped.FirstOrDefault(g => g.Year == m.Year && g.Month == m.Month)?.Count ?? 0)
            .ToArray();

        return SerializeChart(labels, values);
    }

    private async Task<string> BuildTaskStatusAsync(Guid tenantId, IEnumerable<Guid> visibleUserIds)
    {
        var userIdList = visibleUserIds.ToList();
        var groups = await _dbContext.QmsTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId &&
                       t.AssignedToId.HasValue &&
                       userIdList.Contains(t.AssignedToId.Value))
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var labels = new[] { "Open", "InProgress", "Completed", "Overdue" };
        var values = new[]
        {
            groups.FirstOrDefault(g => g.Status == QmsTaskStatus.Open)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == QmsTaskStatus.InProgress)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == QmsTaskStatus.Completed)?.Count ?? 0,
            groups.FirstOrDefault(g => g.Status == QmsTaskStatus.Overdue)?.Count ?? 0
        };

        return SerializeChart(labels, values);
    }
}

