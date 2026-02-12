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
/// Staff dashboard for Junior Staff - personal work view only.
/// </summary>
[Authorize]
public class StaffModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly ILogger<StaffModel> _logger;

    public StaffModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILogger<StaffModel> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _logger = logger;
    }

    public string DisplayName { get; set; } = "Staff";
    public List<StatCard> Stats { get; set; } = new();
    public List<TaskItem> Tasks { get; set; } = new();
    public List<ApprovalItem> PendingApprovals { get; set; } = new();
    public List<DocumentItem> MyDocuments { get; set; } = new();

    public async Task OnGetAsync()
    {
        var currentUser = await GetCurrentUserAsync();
        var tenantId = currentUser?.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();

        if (tenantId == Guid.Empty || currentUser == null)
        {
            return;
        }

        DisplayName = currentUser.FullName;

        // Personal statistics only
        var myDocuments = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && 
            d.CreatedById == currentUser.Id &&
            d.Status != DocumentStatus.Archived);
        
        var myPendingTasks = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId &&
            t.AssignedToId == currentUser.Id &&
            t.Status != QmsTaskStatus.Completed &&
            t.Status != QmsTaskStatus.Cancelled);
        
        var myOverdueTasks = await _dbContext.QmsTasks.CountAsync(t =>
            t.TenantId == tenantId &&
            t.AssignedToId == currentUser.Id &&
            t.DueDate < DateTime.UtcNow &&
            t.Status != QmsTaskStatus.Completed &&
            t.Status != QmsTaskStatus.Cancelled);
        
        var pendingApprovals = await _dbContext.Documents.CountAsync(d =>
            d.TenantId == tenantId && 
            d.CurrentApproverId == currentUser.Id &&
            (d.Status == DocumentStatus.Submitted || d.Status == DocumentStatus.InReview));

        Stats = new List<StatCard>
        {
            new("My documents", myDocuments.ToString(), "Created by you"),
            new("Pending tasks", myPendingTasks.ToString(), "Assigned to you"),
            new("Overdue tasks", myOverdueTasks.ToString(), "Requires attention"),
            new("Awaiting approval", pendingApprovals.ToString(), "Your submissions")
        };

        // Get my tasks directly from database if query fails (graceful fallback)
        try
        {
            var myTasksQuery = new GetMyTasksQuery { Limit = 5 };
            var tasksResult = await _mediator.Send(myTasksQuery);
            if (tasksResult.IsSuccess)
            {
                Tasks = tasksResult.Value.UpcomingTasks
                    .Take(5)
                    .Select(t => new TaskItem(t.Title, t.DueDate?.ToString("MMM dd") ?? "No due date", t.Status.ToString()))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get tasks via query, falling back to direct database query");
            // Fallback: get tasks directly from database
            var directTasks = await _dbContext.QmsTasks
                .AsNoTracking()
                .Where(t => t.TenantId == tenantId && 
                           t.AssignedToId == currentUser.Id &&
                           t.Status != QmsTaskStatus.Completed &&
                           t.Status != QmsTaskStatus.Cancelled)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .Select(t => new TaskItem(t.Title, t.DueDate != null ? t.DueDate.Value.ToString("MMM dd") : "No due date", t.Status.ToString()))
                .ToListAsync();
            Tasks = directTasks;
        }

        // Get pending approvals (documents I submitted)
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

        // My recent documents
        var myDocs = await _dbContext.Documents.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.CreatedById == currentUser.Id)
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new DocumentItem(d.Title, d.DocumentNumber, d.Status.ToString(), d.CreatedAt.ToString("MMM dd, yyyy")))
            .ToListAsync();

        MyDocuments = myDocs;
    }

    public record StatCard(string Title, string Value, string Subtitle);
    public record TaskItem(string Title, string DueDate, string Status);
    public record ApprovalItem(string Title, string Owner, string Stage);
    public record DocumentItem(string Title, string Number, string Status, string Created);

    private async Task<User?> GetCurrentUserAsync()
    {
        if (_currentUserService.UserId.HasValue)
        {
            return await _dbContext.Users
                .Include(u => u.Roles)
                .FirstOrDefaultAsync(u => u.Id == _currentUserService.UserId.Value);
        }

        return null;
    }
}

