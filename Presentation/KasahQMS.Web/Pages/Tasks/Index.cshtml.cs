using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages.Tasks;

[Authorize]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;

    public IndexModel(
        ILogger<IndexModel> logger, 
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Priority { get; set; }

    public int OpenCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int OverdueCount { get; set; }

    /// <summary>
    /// Indicates if current user can create tasks (only managers and above)
    /// </summary>
    public bool CanCreateTask { get; set; }
    
    /// <summary>
    /// The user's current role for display purposes
    /// </summary>
    public string UserRoleContext { get; set; } = "Staff";

    public List<TaskRow> Tasks { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId;

        if (currentUserId == null)
        {
            _logger.LogWarning("Tasks page accessed without valid user context");
            return;
        }

        // Get current user with roles
        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
        {
            _logger.LogWarning("Current user not found: {UserId}", currentUserId);
            return;
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        
        // Determine user's role context and permissions
        bool isAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        bool isTmdOrDeputy = roles.Any(r => r is "TMD" or "TopManagingDirector" or "Country Manager" or "Deputy" or "DeputyDirector" or "Deputy Country Manager");
        bool isManager = roles.Any(r => r.Contains("Manager"));
        bool isAuditor = roles.Any(r => r == "Auditor");

        // Set role context for UI
        if (isAdmin) UserRoleContext = "Admin";
        else if (isTmdOrDeputy) UserRoleContext = "Executive";
        else if (isManager) UserRoleContext = "Manager";
        else if (isAuditor) UserRoleContext = "Auditor";
        else UserRoleContext = "Staff";

        // Only managers and above can create tasks
        CanCreateTask = isAdmin || isTmdOrDeputy || isManager;

        // Build the base query
        var query = _dbContext.QmsTasks.AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.TenantId == tenantId);

        // Apply role-based filtering
        // CRITICAL: This enforces hierarchical visibility
        if (isAdmin || isTmdOrDeputy)
        {
            // Admin/TMD/Deputy can see all tasks
            _logger.LogInformation("User {UserId} has full task visibility (Admin/Executive)", currentUserId);
        }
        else if (isAuditor)
        {
            // Auditors can view all tasks for audit purposes (read-only)
            _logger.LogInformation("User {UserId} has auditor task visibility (read-only)", currentUserId);
        }
        else if (isManager)
        {
            // Managers can see:
            // 1. Tasks they created
            // 2. Tasks assigned to them
            // 3. Tasks assigned to their subordinates (recursively)
            // 4. Tasks assigned to their org unit
            var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(currentUserId.Value);
            var visibleUserIds = new List<Guid> { currentUserId.Value };
            visibleUserIds.AddRange(subordinateIds);

            query = query.Where(t => 
                t.CreatedById == currentUserId.Value ||  // Created by them
                visibleUserIds.Contains(t.AssignedToId ?? Guid.Empty) ||  // Assigned to visible users
                (t.AssignedToOrgUnitId != null && t.AssignedToOrgUnitId == currentUser.OrganizationUnitId));  // Assigned to their org unit

            _logger.LogInformation("Manager {UserId} can see tasks for {Count} users (including subordinates)", 
                currentUserId, visibleUserIds.Count);
        }
        else
        {
            // Staff can only see:
            // 1. Tasks assigned to them
            // 2. Tasks they created
            query = query.Where(t => 
                t.AssignedToId == currentUserId.Value || 
                t.CreatedById == currentUserId.Value);

            _logger.LogInformation("Staff user {UserId} can see only their own tasks", currentUserId);
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            query = query.Where(t => t.Title.Contains(SearchTerm) || t.TaskNumber.Contains(SearchTerm));
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(Status) && Enum.TryParse<QmsTaskStatus>(Status, out var status))
        {
            query = query.Where(t => t.Status == status);
        }

        // Apply priority filter
        if (!string.IsNullOrWhiteSpace(Priority) && Enum.TryParse<TaskPriority>(Priority, out var priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        // Get counts based on the filtered query (respecting authorization)
        OpenCount = await query.CountAsync(t => t.Status == QmsTaskStatus.Open);
        InProgressCount = await query.CountAsync(t => t.Status == QmsTaskStatus.InProgress);
        CompletedCount = await query.CountAsync(t => t.Status == QmsTaskStatus.Completed);
        OverdueCount = await query.CountAsync(t => t.Status == QmsTaskStatus.Overdue);

        Tasks = await query
            .OrderBy(t => t.DueDate ?? DateTime.MaxValue)
            .Select(t => new TaskRow(
                t.Id,
                t.TaskNumber,
                t.Title,
                t.Description,
                t.Priority.ToString(),
                GetPriorityClass(t.Priority),
                t.Status.ToString(),
                GetStatusClass(t.Status),
                t.DueDate.HasValue ? t.DueDate.Value.ToString("MMM dd, yyyy") : "No due date",
                t.AssignedTo != null ? t.AssignedTo.FullName : "Unassigned"))
            .ToListAsync();

        _logger.LogInformation("Tasks page accessed by {UserId} ({RoleContext}) with filters: Search={Search}, Status={Status}, Priority={Priority}. Showing {Count} tasks.",
            currentUserId, UserRoleContext, SearchTerm, Status, Priority, Tasks.Count);
    }

    private static string GetPriorityClass(TaskPriority priority)
    {
        return priority switch
        {
            TaskPriority.High => "bg-rose-100 text-rose-700",
            TaskPriority.Medium => "bg-amber-100 text-amber-700",
            TaskPriority.Low => "bg-emerald-100 text-emerald-700",
            _ => "bg-slate-100 text-slate-600"
        };
    }

    private static string GetStatusClass(QmsTaskStatus status)
    {
        return status switch
        {
            QmsTaskStatus.Completed => "bg-emerald-100 text-emerald-700",
            QmsTaskStatus.InProgress => "bg-brand-100 text-brand-700",
            QmsTaskStatus.Overdue => "bg-rose-100 text-rose-700",
            QmsTaskStatus.Cancelled => "bg-slate-100 text-slate-600",
            _ => "bg-amber-100 text-amber-700"
        };
    }

    public record TaskRow(
        Guid Id,
        string Number,
        string Title,
        string? Description,
        string Priority,
        string PriorityClass,
        string Status,
        string StatusClass,
        string DueDate,
        string Assignee);
}

