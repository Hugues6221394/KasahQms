using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Identity;
using KasahQMS.Infrastructure.Persistence.Data;
using KasahQMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KasahQMS.Web.Pages;

/// <summary>
/// Base page model with authorization helpers.
/// All pages that require authorization should inherit from this.
/// </summary>
public class AuthorizedPageModel : PageModel
{
    protected readonly ApplicationDbContext DbContext;
    protected readonly ICurrentUserService CurrentUserService;
    protected readonly IAuthorizationService AuthorizationService;
    protected readonly IDocumentStateService DocumentStateService;
    protected readonly IAuditLoggingService AuditLoggingService;
    protected readonly IHierarchyService HierarchyService;
    protected readonly ILogger Logger;

    public AuthorizedPageModel(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        IDocumentStateService documentStateService,
        IAuditLoggingService auditLoggingService,
        IHierarchyService hierarchyService,
        ILogger logger)
    {
        DbContext = dbContext;
        CurrentUserService = currentUserService;
        AuthorizationService = authorizationService;
        DocumentStateService = documentStateService;
        AuditLoggingService = auditLoggingService;
        HierarchyService = hierarchyService;
        Logger = logger;
    }

    /// <summary>
    /// Get current authenticated user from database with roles and relationships.
    /// </summary>
    protected async Task<User?> GetCurrentUserAsync()
    {
        var userId = CurrentUserService.UserId;
        if (userId == null) return null;

        return await DbContext.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Include(u => u.OrganizationUnit)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    /// <summary>
    /// Check authorization and handle 403 response.
    /// </summary>
    protected void EnsureAuthorized(bool isAuthorized, string reason = "Access denied")
    {
        if (!isAuthorized)
        {
            Logger.LogWarning("User {UserId} unauthorized: {Reason}", CurrentUserService.UserId, reason);
        }
    }

    /// <summary>
    /// Redirect to access denied page.
    /// </summary>
    protected RedirectToPageResult AccessDenied()
    {
        Logger.LogWarning("Access denied for user {UserId} on {Page}", 
            CurrentUserService.UserId, Request.Path);
        return RedirectToPage("/Error");
    }

    /// <summary>
    /// Check if user is auditor (read-only).
    /// </summary>
    protected async Task<bool> IsAuditorAsync()
    {
        if (CurrentUserService.UserId == null) return false;
        return await AuthorizationService.IsAuditorAsync(CurrentUserService.UserId.Value);
    }

    /// <summary>
    /// Check if user is admin/TMD.
    /// </summary>
    protected async Task<bool> IsAdminAsync()
    {
        if (CurrentUserService.UserId == null) return false;
        return await AuthorizationService.IsAdminAsync(CurrentUserService.UserId.Value);
    }

    /// <summary>
    /// Check if user can view a subordinate.
    /// </summary>
    protected async Task<bool> CanViewSubordinateAsync(Guid targetUserId)
    {
        if (CurrentUserService.UserId == null) return false;
        return await AuthorizationService.CanViewSubordinateAsync(CurrentUserService.UserId.Value, targetUserId);
    }

    /// <summary>
    /// Get subordinate user IDs for current user.
    /// </summary>
    protected async Task<List<Guid>> GetSubordinatesAsync()
    {
        if (CurrentUserService.UserId == null) return new List<Guid>();
        return await AuthorizationService.GetSubordinateUserIdsAsync(CurrentUserService.UserId.Value);
    }
}
