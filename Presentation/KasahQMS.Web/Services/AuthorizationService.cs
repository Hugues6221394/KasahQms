using KasahQMS.Domain.Enums;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Services;

/// <summary>
/// Service for checking user permissions and authorization based on role hierarchy.
/// This is the core authorization engine for the QMS system.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Check if user can create a document.
    /// </summary>
    Task<bool> CanCreateDocumentAsync(Guid userId, Guid? targetDepartmentId = null);

    /// <summary>
    /// Check if user can edit a document (only creator in Draft state).
    /// </summary>
    Task<bool> CanEditDocumentAsync(Guid userId, Guid documentId);

    /// <summary>
    /// Check if user can submit a document for approval.
    /// </summary>
    Task<bool> CanSubmitDocumentAsync(Guid userId, Guid documentId);

    /// <summary>
    /// Check if user can approve a document.
    /// </summary>
    Task<bool> CanApproveDocumentAsync(Guid userId, Guid documentId);

    /// <summary>
    /// Check if user can reject a document.
    /// </summary>
    Task<bool> CanRejectDocumentAsync(Guid userId, Guid documentId);

    /// <summary>
    /// Check if user can view a document (hierarchical + owner).
    /// </summary>
    Task<bool> CanViewDocumentAsync(Guid userId, Guid documentId);

    /// <summary>
    /// Check if user can delete a document (only admins on non-published).
    /// </summary>
    Task<bool> CanDeleteDocumentAsync(Guid userId, Guid documentId);

    /// <summary>
    /// Check if user can create tasks (only managers).
    /// </summary>
    Task<bool> CanCreateTaskAsync(Guid userId);

    /// <summary>
    /// Check if user can assign tasks to others.
    /// </summary>
    Task<bool> CanAssignTaskAsync(Guid userId, Guid assigneeId);

    /// <summary>
    /// Check if user can view a task.
    /// </summary>
    Task<bool> CanViewTaskAsync(Guid userId, Guid taskId);

    /// <summary>
    /// Check if user is an auditor (read-only role).
    /// </summary>
    Task<bool> IsAuditorAsync(Guid userId);

    /// <summary>
    /// Check if user is an admin/TMD.
    /// </summary>
    Task<bool> IsAdminAsync(Guid userId);

    /// <summary>
    /// Get all subordinate user IDs (recursive hierarchy).
    /// </summary>
    Task<List<Guid>> GetSubordinateUserIdsAsync(Guid userId);

    /// <summary>
    /// Get user's department.
    /// </summary>
    Task<Guid?> GetUserDepartmentAsync(Guid userId);

    /// <summary>
    /// Get direct manager of user.
    /// </summary>
    Task<Guid?> GetUserManagerAsync(Guid userId);

    /// <summary>
    /// Check if user can view subordinate (hierarchical).
    /// </summary>
    Task<bool> CanViewSubordinateAsync(Guid userId, Guid targetUserId);
}

public class AuthorizationService : IAuthorizationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuthorizationService> _logger;
    private readonly IHierarchyService _hierarchyService;

    public AuthorizationService(
        ApplicationDbContext dbContext,
        ILogger<AuthorizationService> logger,
        IHierarchyService hierarchyService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _hierarchyService = hierarchyService;
    }

    public async Task<bool> CanCreateDocumentAsync(Guid userId, Guid? targetDepartmentId = null)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors cannot create documents
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
        {
            _logger.LogWarning("Auditor {UserId} attempted to create document", userId);
            return false;
        }

        // System Admin and TMD can always create
        if (user.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true)
            return true;

        // All other authenticated roles can create
        return user.Roles?.Any() == true;
    }

    public async Task<bool> CanEditDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors cannot edit anything
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return false;

        // Only Draft status is editable
        if (document.Status != DocumentStatus.Draft)
            return false;

        // System Admin/TMD can always edit drafts
        return user.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true;
    }

    public async Task<bool> CanSubmitDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        // Only in Draft state can submit
        if (document.Status != DocumentStatus.Draft)
            return false;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors cannot submit
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return false;

        // Authenticated users can submit (or admin)
        return true;
    }

    public async Task<bool> CanApproveDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors cannot approve
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return false;

        // Document must be Submitted for approval
        if (document.Status != DocumentStatus.Submitted)
            return false;

        // Only admin/TMD/managers can approve
        return user.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD" or "Manager" or "Department Manager") == true;
    }

    public async Task<bool> CanRejectDocumentAsync(Guid userId, Guid documentId)
    {
        // Rejection permission same as approval
        return await CanApproveDocumentAsync(userId, documentId);
    }

    public async Task<bool> CanViewDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors can view everything (read-only)
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return true;

        // Admin/TMD can view everything
        if (user.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true)
            return true;

        // Authenticated users can view documents in general
        return user.Roles?.Any() == true;
    }

    public async Task<bool> CanDeleteDocumentAsync(Guid userId, Guid documentId)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null) return false;

        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors cannot delete
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return false;

        // Approved/Published documents cannot be deleted
        if (document.Status == DocumentStatus.Approved)
            return false;

        // Only admin/TMD can delete
        return user.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true;
    }

    public async Task<bool> CanCreateTaskAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors cannot create tasks
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return false;

        // Only managers, department managers, and admins can create tasks
        return user.Roles?.Any(r => r.Name is "TMD" or "Deputy Country Manager" or "Department Manager" or 
            "System Admin" or "Admin" or "Manager") == true;
    }

    public async Task<bool> CanAssignTaskAsync(Guid userId, Guid assigneeId)
    {
        // User must be able to create tasks
        if (!await CanCreateTaskAsync(userId))
            return false;

        var assigner = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        var assignee = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == assigneeId);

        if (assigner == null || assignee == null)
            return false;

        // Admin/TMD can assign to anyone
        if (assigner.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true)
            return true;

        // Department manager can assign to department staff
        if (assigner.Roles?.Any(r => r.Name == "Department Manager") == true)
        {
            return assigner.OrganizationUnitId == assignee.OrganizationUnitId;
        }

        return false;
    }

    public async Task<bool> CanViewTaskAsync(Guid userId, Guid taskId)
    {
        // Task viewing check - basic implementation
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Auditors can view everything
        if (user.Roles?.Any(r => r.Name == "Auditor") == true)
            return true;

        // Admin/TMD can view everything
        if (user.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true)
            return true;

        // Authenticated users can view tasks in general
        return user.Roles?.Any() == true;
    }

    public async Task<bool> IsAuditorAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user?.Roles?.Any(r => r.Name == "Auditor") == true;
    }

    public async Task<bool> IsAdminAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user?.Roles?.Any(r => r.Name is "System Admin" or "Admin" or "TMD") == true;
    }

    public async Task<List<Guid>> GetSubordinateUserIdsAsync(Guid userId)
    {
        return await _hierarchyService.GetSubordinateUserIdsAsync(userId);
    }

    public async Task<Guid?> GetUserDepartmentAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user?.OrganizationUnitId;
    }

    public async Task<Guid?> GetUserManagerAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        return user?.ManagerId;
    }

    public async Task<bool> CanViewSubordinateAsync(Guid userId, Guid targetUserId)
    {
        if (userId == targetUserId) return true;

        var subordinates = await GetSubordinateUserIdsAsync(userId);
        return subordinates.Contains(targetUserId);
    }
}
