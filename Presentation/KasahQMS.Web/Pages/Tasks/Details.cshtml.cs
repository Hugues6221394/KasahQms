using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Application.Features.Tasks.Commands;
using KasahQMS.Domain.Entities.Tasks;
using KasahQMS.Infrastructure.Persistence.Data;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Notification = KasahQMS.Domain.Entities.Notifications.Notification;

namespace KasahQMS.Web.Pages.Tasks;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IAuditLogService _auditLogService;
    private readonly IRealTimeNotificationService _realTimeNotificationService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        ApplicationDbContext dbContext,
        IMediator mediator,
        ICurrentUserService currentUserService,
        IHierarchyService hierarchyService,
        IAuditLogService auditLogService,
        IRealTimeNotificationService realTimeNotificationService,
        ILogger<DetailsModel> logger)
    {
        _dbContext = dbContext;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _hierarchyService = hierarchyService;
        _auditLogService = auditLogService;
        _realTimeNotificationService = realTimeNotificationService;
        _logger = logger;
    }

    public TaskDetailView? TaskItem { get; set; }
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }
    
    /// <summary>
    /// Indicates if current user is viewing in read-only mode (auditors)
    /// </summary>
    public bool IsReadOnly { get; set; }
    
    /// <summary>
    /// Indicates if current user is the supervisor (created the task)
    /// </summary>
    public bool IsSupervisor { get; set; }
    
    /// <summary>
    /// The user's role context for display purposes
    /// </summary>
    public string UserRoleContext { get; set; } = "Staff";
    
    /// <summary>
    /// Activity history for the task
    /// </summary>
    public List<TaskActivityInfo> Activities { get; set; } = new();
    
    /// <summary>
    /// Linked document details for preview
    /// </summary>
    public LinkedDocumentInfo? LinkedDocument { get; set; }

    [BindProperty] public string? CompletionNotes { get; set; }
    [BindProperty] public string? ActivityNote { get; set; }
    [BindProperty] public int? ProgressPercentage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        var tenantId = _currentUserService.TenantId ?? await _dbContext.Tenants.Select(t => t.Id).FirstOrDefaultAsync();
        var currentUserId = _currentUserService.UserId;

        if (currentUserId == null)
        {
            _logger.LogWarning("Task details accessed without valid user context");
            return Unauthorized();
        }

        // Get task with basic info
        var task = await _dbContext.QmsTasks.AsNoTracking()
            .Include(t => t.AssignedTo)
            .Include(t => t.CompletedBy)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

        if (task == null) return NotFound();

        // Get current user with roles for authorization check
        var currentUser = await _dbContext.Users.AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == currentUserId);

        if (currentUser == null)
        {
            _logger.LogWarning("Current user not found: {UserId}", currentUserId);
            return Unauthorized();
        }

        var roles = currentUser.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        
        // Determine user's role context
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

        IsReadOnly = isAuditor;

        // AUTHORIZATION CHECK: Can this user view this task?
        bool canView = false;

        if (isAdmin || isTmdOrDeputy)
        {
            // Admin/TMD/Deputy can view all tasks
            canView = true;
        }
        else if (isAuditor)
        {
            // Auditors can view all tasks (read-only for audit purposes)
            canView = true;
        }
        else if (task.AssignedToId == currentUserId || task.CreatedById == currentUserId)
        {
            // Assignee or creator can always view
            canView = true;
        }
        else if (isManager)
        {
            // Managers can view tasks assigned to their subordinates
            if (task.AssignedToId.HasValue)
            {
                var subordinateIds = await _hierarchyService.GetSubordinateUserIdsAsync(currentUserId.Value);
                canView = subordinateIds.Contains(task.AssignedToId.Value);
            }
            
            // Also check if task is assigned to their org unit
            if (!canView && task.AssignedToOrgUnitId.HasValue && 
                task.AssignedToOrgUnitId == currentUser.OrganizationUnitId)
            {
                canView = true;
            }
        }

        if (!canView)
        {
            _logger.LogWarning("User {UserId} attempted to access task {TaskId} without authorization", 
                currentUserId, id);
            return Forbid();
        }

        string? linkedDocTitle = null;
        if (task.LinkedDocumentId.HasValue)
        {
            linkedDocTitle = await _dbContext.Documents.AsNoTracking()
                .Where(d => d.Id == task.LinkedDocumentId.Value)
                .Select(d => d.Title)
                .FirstOrDefaultAsync();
        }

        // Get creator name
        var creatorName = await _dbContext.Users.AsNoTracking()
            .Where(u => u.Id == task.CreatedById)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync() ?? "Unknown";

        TaskItem = new TaskDetailView(
            task.Id,
            task.TaskNumber,
            task.Title,
            task.Description,
            task.Status.ToString(),
            task.Priority.ToString(),
            task.DueDate?.ToString("MMM dd, yyyy"),
            task.AssignedTo != null ? (task.AssignedTo.FirstName + " " + task.AssignedTo.LastName) : "Unassigned",
            task.LinkedDocumentId,
            linkedDocTitle,
            task.AssignedToId,
            task.CreatedById,
            creatorName,
            task.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
            task.CompletedAt?.ToString("MMM dd, yyyy HH:mm"),
            task.CompletionNotes,
            task.ReviewerRemarks
        );
        
        // Check if current user is supervisor
        IsSupervisor = task.CreatedById == currentUserId;

        Attachments = await _dbContext.TaskAttachments.AsNoTracking()
            .Where(a => a.TaskId == id)
            .OrderBy(a => a.FileName)
            .Select(a => new TaskAttachmentInfo(a.Id, a.FileName, a.StoragePath, a.ContentType, a.SizeBytes ?? 0))
            .ToListAsync();

        // Load activity history
        Activities = await _dbContext.TaskActivities.AsNoTracking()
            .Where(a => a.TaskId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Include(a => a.User)
            .Select(a => new TaskActivityInfo(
                a.Id,
                a.ActivityType,
                a.Description,
                a.User != null ? a.User.FirstName + " " + a.User.LastName : "Unknown",
                a.CreatedAt.ToString("MMM dd, yyyy HH:mm"),
                a.ProgressPercentage,
                a.AttachmentName
            ))
            .ToListAsync();

        // Load linked document details for preview
        if (task.LinkedDocumentId.HasValue)
        {
            var doc = await _dbContext.Documents.AsNoTracking()
                .Include(d => d.DocumentType)
                .Where(d => d.Id == task.LinkedDocumentId.Value)
                .Select(d => new LinkedDocumentInfo(
                    d.Id,
                    d.DocumentNumber,
                    d.Title,
                    d.Description,
                    d.Status.ToString(),
                    d.DocumentType != null ? d.DocumentType.Name : "—",
                    d.FilePath,
                    d.OriginalFileName
                ))
                .FirstOrDefaultAsync();
            LinkedDocument = doc;
        }

        _logger.LogInformation("User {UserId} ({RoleContext}) viewed task {TaskId}", 
            currentUserId, UserRoleContext, id);

        return Page();
    }
    
    public async Task<IActionResult> OnPostAddActivityAsync(Guid id)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null)
            return Unauthorized();
        
        if (string.IsNullOrWhiteSpace(ActivityNote))
        {
            return RedirectToPage(new { id, message = "Activity note is required.", success = false });
        }
        
        var task = await _dbContext.QmsTasks
            .Include(t => t.AssignedTo)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (task == null)
            return NotFound();
        
        // Only the assignee can add activity updates
        if (task.AssignedToId != currentUserId)
        {
            _logger.LogWarning("User {UserId} attempted to add activity to task {TaskId} they are not assigned to", 
                currentUserId, id);
            return RedirectToPage(new { id, message = "Only the assignee can add progress updates.", success = false });
        }
        
        // Cannot add updates to completed or cancelled tasks
        if (task.Status == Domain.Enums.QmsTaskStatus.Completed || task.Status == Domain.Enums.QmsTaskStatus.Cancelled)
        {
            return RedirectToPage(new { id, message = "Cannot add updates to completed or cancelled tasks.", success = false });
        }
        
        // Get the current user's name for the notification
        var currentUser = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUserId.Value);
        var assigneeName = currentUser?.FullName ?? "Assignee";
        
        // Create activity record
        var activity = TaskActivity.Create(
            id,
            currentUserId.Value,
            ProgressPercentage.HasValue ? "Progress Update" : "Note",
            ActivityNote,
            ProgressPercentage
        );
        
        _dbContext.TaskActivities.Add(activity);
        await _dbContext.SaveChangesAsync();
        
        // Notify the task creator (assigner) about the update
        if (task.CreatedById != currentUserId.Value)
        {
            // Create database notification
            var notificationTitle = ProgressPercentage.HasValue 
                ? $"Task Progress Update: {ProgressPercentage}%"
                : "Task Update Received";
            var notificationMessage = $"{assigneeName} posted an update on task \"{task.Title}\": {(ActivityNote.Length > 100 ? ActivityNote.Substring(0, 97) + "..." : ActivityNote)}";
            
            var notification = Notification.Create(
                task.CreatedById,
                notificationTitle,
                notificationMessage,
                Domain.Entities.Notifications.NotificationType.TaskUpdate,
                task.Id,
                "Task"
            );
            
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();
            
            // Send real-time notification to the task creator (assigner)
            await _realTimeNotificationService.NotifyTaskUpdateAsync(
                task.CreatedById,
                task.Id,
                task.Title,
                assigneeName,
                ActivityNote,
                ProgressPercentage,
                CancellationToken.None);
            
            _logger.LogInformation("Notification sent to task creator {CreatorId} about update on task {TaskId}", 
                task.CreatedById, id);
        }
        
        // Log the activity
        await _auditLogService.LogAsync(
            "TASK_ACTIVITY_ADDED",
            "Tasks",
            id,
            $"Activity added to task: {ActivityNote}",
            CancellationToken.None);
        
        _logger.LogInformation("User {UserId} added activity to task {TaskId}", currentUserId, id);
        
        return RedirectToPage(new { id, message = "Activity recorded.", success = true });
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var result = await _mediator.Send(new ApproveTaskCommand(id));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Task approved." : result.ErrorMessage, success = result.IsSuccess });
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, string remarks)
    {
        var result = await _mediator.Send(new RejectTaskCommand(id, remarks));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Task rejected." : result.ErrorMessage, success = result.IsSuccess });
    }

    public List<TaskAttachmentInfo> Attachments { get; set; } = new();

    public record TaskAttachmentInfo(Guid Id, string FileName, string FilePath, string? FileType, long FileSizeBytes);

    public async Task<IActionResult> OnPostCompleteAsync(Guid id)
    {
        var result = await _mediator.Send(new CompleteTaskCommand(id, CompletionNotes));
        return RedirectToPage(new { id, message = result.IsSuccess ? "Task marked complete." : result.ErrorMessage, success = result.IsSuccess });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        var result = await _mediator.Send(new DeleteTaskCommand(id));
        if (result.IsSuccess)
            return RedirectToPage("./Index", new { message = "Task deleted.", success = true });
        return RedirectToPage(new { id, message = result.ErrorMessage ?? "Delete failed.", success = false });
    }

    /// <summary>
    /// Indicates if current user is the assignee of this task.
    /// </summary>
    public bool IsAssignee => TaskItem != null && _currentUserService.UserId == TaskItem.AssignedToId;

    /// <summary>
    /// Indicates if current user can add activity updates to this task.
    /// Only assignee can add updates, and only if not read-only and task is not completed/cancelled.
    /// </summary>
    public bool CanAddActivity => TaskItem != null
        && !IsReadOnly
        && IsAssignee
        && TaskItem.Status != "Completed" && TaskItem.Status != "Cancelled";

    /// <summary>
    /// Indicates if current user can complete this task.
    /// Only assignee can complete, and only if not already completed/cancelled.
    /// Auditors cannot complete tasks (read-only).
    /// </summary>
    public bool CanComplete => TaskItem != null
        && !IsReadOnly
        && IsAssignee
        && TaskItem.Status != "Completed" && TaskItem.Status != "Cancelled";

    /// <summary>
    /// Indicates if current user can edit or delete this task.
    /// Only creator can edit/delete, and only if not already completed/cancelled.
    /// Auditors cannot edit/delete (read-only).
    /// </summary>
    public bool CanEditOrDelete => TaskItem != null
        && !IsReadOnly
        && _currentUserService.UserId == TaskItem.CreatedById
        && TaskItem.Status != "Completed" && TaskItem.Status != "Cancelled";

    /// <summary>
    /// Indicates if current user can approve the task completion (for managers).
    /// Managers can approve task completion for their subordinates.
    /// </summary>
    public bool CanApproveCompletion => TaskItem != null 
        && !IsReadOnly
        && (UserRoleContext is "Admin" or "Executive" or "Manager")
        && TaskItem.Status == "AwaitingApproval"
        && _currentUserService.UserId != TaskItem.AssignedToId;

    /// <summary>True when current user is creator or assignee and there is another party to message.</summary>
    public bool CanMessage => TaskItem != null
        && !IsReadOnly
        && _currentUserService.UserId != null
        && ((TaskItem.CreatedById == _currentUserService.UserId && TaskItem.AssignedToId.HasValue && TaskItem.AssignedToId != _currentUserService.UserId)
            || (TaskItem.AssignedToId == _currentUserService.UserId && TaskItem.CreatedById != _currentUserService.UserId));

    /// <summary>The other user to message (assignee if we're creator, creator if we're assignee).</summary>
    public Guid? MessageOtherUserId => TaskItem == null || _currentUserService.UserId == null ? null
        : TaskItem.CreatedById == _currentUserService.UserId ? TaskItem.AssignedToId
        : TaskItem.AssignedToId == _currentUserService.UserId ? TaskItem.CreatedById
        : (Guid?)null;

    public record TaskDetailView(
        Guid Id,
        string Number,
        string Title,
        string? Description,
        string Status,
        string Priority,
        string? DueDate,
        string Assignee,
        Guid? LinkedDocId,
        string? LinkedDocTitle,
        Guid? AssignedToId,
        Guid CreatedById,
        string CreatorName,
        string CreatedAt,
        string? CompletedAt,
        string? CompletionNotes,
        string? ReviewerRemarks
    );
    
    public record TaskActivityInfo(
        Guid Id,
        string ActivityType,
        string Description,
        string UserName,
        string CreatedAt,
        int? ProgressPercentage,
        string? AttachmentName
    );
    
    public record LinkedDocumentInfo(
        Guid Id,
        string DocumentNumber,
        string Title,
        string? Description,
        string Status,
        string DocumentType,
        string? FilePath,
        string? OriginalFileName
    );
}
