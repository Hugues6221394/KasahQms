using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages;

[Authorize]
public class ArchivesModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ArchivesModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public List<ArchivedTaskRow> ArchivedTasks { get; set; } = new();
    public List<ArchivedDocumentRow> ArchivedDocuments { get; set; } = new();
    public string ActiveTab { get; set; } = "tasks";
    public string? ActionMessage { get; set; }
    public bool? ActionSuccess { get; set; }
    public bool CanViewActorInfo { get; set; }

    public async Task<IActionResult> OnGetAsync(string? tab = "tasks", string? message = null, bool? success = null)
    {
        ActionMessage = message;
        ActionSuccess = success;
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;

        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        ActiveTab = tab ?? "tasks";

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId.Value);

        if (user == null)
            return RedirectToPage("/Account/Login");

        var roles = user.Roles?.Select(r => r.Name).ToList() ?? new List<string>();
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager");
        var isSystemAdmin = roles.Any(r => r is "System Admin" or "Admin" or "SystemAdmin" or "TenantAdmin");
        var isPrivilegedViewer = isTmdOrDeputy || isSystemAdmin;
        var isManager = roles.Any(r => r.Contains("Manager", StringComparison.OrdinalIgnoreCase)) || isPrivilegedViewer;
        CanViewActorInfo = isTmdOrDeputy || isSystemAdmin;

        // Load archived tasks
        var taskQuery = _dbContext.QmsTasks
            .AsNoTracking()
            .Include(t => t.AssignedTo)
            .Where(t => t.TenantId == tenantId && t.Status == QmsTaskStatus.Archived);

        // Filter based on role
        if (!isPrivilegedViewer)
        {
            if (isManager && user.OrganizationUnitId.HasValue)
            {
                // Managers see archived tasks from their department
                taskQuery = taskQuery.Where(t =>
                    (t.AssignedTo != null && t.AssignedTo.OrganizationUnitId == user.OrganizationUnitId.Value) ||
                    t.AssignedToId == userId.Value ||
                    t.CreatedById == userId.Value);
            }
            else
            {
                // Staff see only their own archived tasks
                taskQuery = taskQuery.Where(t => t.AssignedToId == userId.Value || t.CreatedById == userId.Value);
            }
        }

        var taskRows = await taskQuery
            .OrderByDescending(t => t.LastModifiedAt)
            .Select(t => new
            {
                t.Id,
                t.TaskNumber,
                t.Title,
                Priority = t.Priority.ToString(),
                AssignedTo = t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : "—",
                CompletedAt = t.CompletedAt,
                ArchivedAt = t.LastModifiedAt,
                t.CreatedAt,
                t.CreatedById,
                ArchivedById = t.LastModifiedById,
                ArchivedByName = _dbContext.Users
                    .Where(u => u.Id == t.LastModifiedById)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
                t.IsDeleted,
                t.DeletedAt,
                t.DeletedById,
                DeletedByName = _dbContext.Users
                    .Where(u => u.Id == t.DeletedById)
                    .Select(u => u.FullName)
                    .FirstOrDefault()
            })
            .ToListAsync();

        ArchivedTasks = taskRows
            .Select(t => new ArchivedTaskRow(
                t.Id,
                t.TaskNumber,
                t.Title,
                t.Priority,
                t.AssignedTo,
                t.CompletedAt.HasValue ? t.CompletedAt.Value.ToString("MMM dd, yyyy") : "—",
                t.ArchivedAt.HasValue ? t.ArchivedAt.Value.ToString("MMM dd, yyyy") : t.CreatedAt.ToString("MMM dd, yyyy"),
                t.IsDeleted ? "Deleted" : "Archived",
                t.ArchivedByName ?? "Unknown",
                t.DeletedByName,
                CanMutateArchive(userId.Value, t.CreatedById, t.ArchivedById, isTmdOrDeputy) && !t.IsDeleted,
                CanMutateArchive(userId.Value, t.CreatedById, t.ArchivedById, isTmdOrDeputy) && !t.IsDeleted
            ))
            .ToList();

        // Load archived documents
        var docQuery = _dbContext.Documents
            .AsNoTracking()
            .Include(d => d.DocumentType)
            .Where(d => d.TenantId == tenantId && d.Status == DocumentStatus.Archived);

        // Similar filtering for documents
        if (!isPrivilegedViewer)
        {
            if (isManager && user.OrganizationUnitId.HasValue)
            {
                docQuery = docQuery.Where(d => d.CreatedById == userId.Value);
            }
            else
            {
                docQuery = docQuery.Where(d => d.CreatedById == userId.Value);
            }
        }

        var docRows = await docQuery
            .OrderByDescending(d => d.LastModifiedAt)
            .Select(d => new
            {
                d.Id,
                d.DocumentNumber,
                d.Title,
                Type = d.DocumentType != null ? d.DocumentType.Name : "—",
                d.LastModifiedAt,
                d.CreatedAt,
                d.CreatedById,
                d.ArchivedById,
                ArchivedByName = _dbContext.Users
                    .Where(u => u.Id == d.ArchivedById)
                    .Select(u => u.FullName)
                    .FirstOrDefault(),
                d.IsDeleted,
                d.DeletedAt,
                d.DeletedById,
                DeletedByName = _dbContext.Users
                    .Where(u => u.Id == d.DeletedById)
                    .Select(u => u.FullName)
                    .FirstOrDefault()
            })
            .ToListAsync();

        ArchivedDocuments = docRows
            .Select(d => new ArchivedDocumentRow(
                d.Id,
                d.DocumentNumber,
                d.Title,
                d.Type,
                d.LastModifiedAt.HasValue ? d.LastModifiedAt.Value.ToString("MMM dd, yyyy") : d.CreatedAt.ToString("MMM dd, yyyy"),
                d.IsDeleted ? "Deleted" : "Archived",
                d.ArchivedByName ?? "Unknown",
                d.DeletedByName,
                CanMutateArchive(userId.Value, d.CreatedById, d.ArchivedById, isTmdOrDeputy) && !d.IsDeleted,
                CanMutateArchive(userId.Value, d.CreatedById, d.ArchivedById, isTmdOrDeputy) && !d.IsDeleted
            ))
            .ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostRestoreTaskAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        var roles = await GetUserRoleNamesAsync(userId.Value);
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager");

        var task = await _dbContext.QmsTasks.FirstOrDefaultAsync(t =>
            t.Id == id && t.TenantId == tenantId.Value && t.Status == QmsTaskStatus.Archived);
        if (task == null)
            return RedirectToPage(new { tab = "tasks", message = "Archived task not found.", success = false });
        if (task.IsDeleted)
            return RedirectToPage(new { tab = "tasks", message = "Deleted tasks cannot be restored.", success = false });
        if (!CanMutateArchive(userId.Value, task.CreatedById, task.LastModifiedById, isTmdOrDeputy))
            return RedirectToPage(new { tab = "tasks", message = "You are not allowed to restore this task.", success = false });

        task.Status = QmsTaskStatus.Completed;
        task.LastModifiedById = userId.Value;
        task.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { tab = "tasks", message = "Task restored successfully.", success = true });
    }

    public async Task<IActionResult> OnPostPermanentDeleteTaskAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        var roles = await GetUserRoleNamesAsync(userId.Value);
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager");

        var task = await _dbContext.QmsTasks.FirstOrDefaultAsync(t =>
            t.Id == id && t.TenantId == tenantId.Value && t.Status == QmsTaskStatus.Archived);
        if (task == null)
            return RedirectToPage(new { tab = "tasks", message = "Archived task not found.", success = false });
        if (task.IsDeleted)
            return RedirectToPage(new { tab = "tasks", message = "Task is already deleted.", success = false });
        if (!CanMutateArchive(userId.Value, task.CreatedById, task.LastModifiedById, isTmdOrDeputy))
            return RedirectToPage(new { tab = "tasks", message = "You are not allowed to delete this task.", success = false });

        task.IsDeleted = true;
        task.DeletedAt = DateTime.UtcNow;
        task.DeletedById = userId.Value;
        task.LastModifiedById = userId.Value;
        task.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { tab = "tasks", message = "Task permanently deleted.", success = true });
    }

    public async Task<IActionResult> OnPostRestoreDocumentAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        var roles = await GetUserRoleNamesAsync(userId.Value);
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager");

        var doc = await _dbContext.Documents.FirstOrDefaultAsync(d =>
            d.Id == id && d.TenantId == tenantId.Value && d.Status == DocumentStatus.Archived);
        if (doc == null)
            return RedirectToPage(new { tab = "documents", message = "Archived document not found.", success = false });
        if (doc.IsDeleted)
            return RedirectToPage(new { tab = "documents", message = "Deleted documents cannot be restored.", success = false });
        if (!CanMutateArchive(userId.Value, doc.CreatedById, doc.ArchivedById, isTmdOrDeputy))
            return RedirectToPage(new { tab = "documents", message = "You are not allowed to restore this document.", success = false });

        doc.Status = DocumentStatus.Approved;
        doc.ArchivedAt = null;
        doc.ArchivedById = null;
        doc.ArchiveReason = null;
        doc.LastModifiedById = userId.Value;
        doc.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { tab = "documents", message = "Document restored successfully.", success = true });
    }

    public async Task<IActionResult> OnPostPermanentDeleteDocumentAsync(Guid id)
    {
        var userId = _currentUserService.UserId;
        var tenantId = _currentUserService.TenantId;
        if (userId == null || tenantId == null)
            return RedirectToPage("/Account/Login");

        var roles = await GetUserRoleNamesAsync(userId.Value);
        var isTmdOrDeputy = roles.Any(r => r == "TMD" || r == "Deputy" || r == "Deputy Country Manager");

        var doc = await _dbContext.Documents.FirstOrDefaultAsync(d =>
            d.Id == id && d.TenantId == tenantId.Value && d.Status == DocumentStatus.Archived);
        if (doc == null)
            return RedirectToPage(new { tab = "documents", message = "Archived document not found.", success = false });
        if (doc.IsDeleted)
            return RedirectToPage(new { tab = "documents", message = "Document is already deleted.", success = false });
        if (!CanMutateArchive(userId.Value, doc.CreatedById, doc.ArchivedById, isTmdOrDeputy))
            return RedirectToPage(new { tab = "documents", message = "You are not allowed to delete this document.", success = false });

        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;
        doc.DeletedById = userId.Value;
        doc.LastModifiedById = userId.Value;
        doc.LastModifiedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return RedirectToPage(new { tab = "documents", message = "Document permanently deleted.", success = true });
    }

    private static bool CanMutateArchive(Guid currentUserId, Guid createdById, Guid? archivedById, bool isTmdOrDeputy)
    {
        var isCreator = createdById == currentUserId;
        var isArchiver = archivedById == currentUserId;

        // TMD/Deputy can see all archives, but can only mutate archives they own.
        if (isTmdOrDeputy)
            return isCreator || isArchiver;

        // Default rule: archive owner or creator can restore/permanently delete.
        return isCreator || isArchiver;
    }

    private async Task<List<string>> GetUserRoleNamesAsync(Guid userId)
    {
        return await _dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(_dbContext.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync();
    }

    public record ArchivedTaskRow(
        Guid Id,
        string TaskNumber,
        string Title,
        string Priority,
        string AssignedTo,
        string CompletedAt,
        string ArchivedAt,
        string Lifecycle,
        string ArchivedBy,
        string? DeletedBy,
        bool CanRestore,
        bool CanPermanentDelete
    );

    public record ArchivedDocumentRow(
        Guid Id,
        string DocumentNumber,
        string Title,
        string Type,
        string ArchivedAt,
        string Lifecycle,
        string ArchivedBy,
        string? DeletedBy,
        bool CanRestore,
        bool CanPermanentDelete
    );
}
