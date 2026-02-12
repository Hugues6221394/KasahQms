using KasahQMS.Application.Common.Interfaces;
using KasahQMS.Application.Common.Interfaces.Services;

namespace KasahQMS.Web.Services;

/// <summary>
/// Service for comprehensive audit logging of all user actions.
/// Creates immutable audit trail for compliance and forensics.
/// </summary>
public interface IAuditLoggingService
{
    /// <summary>
    /// Log a user action (generic).
    /// </summary>
    Task LogActionAsync(string action, string entity, Guid? entityId, string? details = null, bool success = true);

    /// <summary>
    /// Log document creation.
    /// </summary>
    Task LogDocumentCreatedAsync(Guid documentId, string title);

    /// <summary>
    /// Log document submission.
    /// </summary>
    Task LogDocumentSubmittedAsync(Guid documentId);

    /// <summary>
    /// Log document approval.
    /// </summary>
    Task LogDocumentApprovedAsync(Guid documentId, string? comments = null);

    /// <summary>
    /// Log document rejection.
    /// </summary>
    Task LogDocumentRejectedAsync(Guid documentId, string? reason = null);

    /// <summary>
    /// Log document edited.
    /// </summary>
    Task LogDocumentEditedAsync(Guid documentId);

    /// <summary>
    /// Log task created.
    /// </summary>
    Task LogTaskCreatedAsync(Guid taskId, string title, Guid? assignedToId);

    /// <summary>
    /// Log task completed.
    /// </summary>
    Task LogTaskCompletedAsync(Guid taskId);

    /// <summary>
    /// Log task rejected.
    /// </summary>
    Task LogTaskRejectedAsync(Guid taskId, string? reason = null);

    /// <summary>
    /// Log user login.
    /// </summary>
    Task LogUserLoginAsync(Guid userId);

    /// <summary>
    /// Log user logout.
    /// </summary>
    Task LogUserLogoutAsync(Guid userId);

    /// <summary>
    /// Log failed login attempt.
    /// </summary>
    Task LogFailedLoginAsync(string username);
}

public class AuditLoggingService : IAuditLoggingService
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditLogService _persistentAuditLogService;
    private readonly ILogger<AuditLoggingService> _logger;

    public AuditLoggingService(
        ICurrentUserService currentUserService,
        IAuditLogService persistentAuditLogService,
        ILogger<AuditLoggingService> logger)
    {
        _currentUserService = currentUserService;
        _persistentAuditLogService = persistentAuditLogService;
        _logger = logger;
    }

    public async Task LogActionAsync(string action, string entity, Guid? entityId, string? details = null, bool success = true)
    {
        try
        {
            var logMessage = $"[{action}] Entity: {entity}, EntityId: {entityId}, UserId: {_currentUserService.UserId}, " +
                $"Success: {success}, IP: {_currentUserService.IpAddress}";
            
            if (!string.IsNullOrEmpty(details))
                logMessage += $", Details: {details}";

            if (success)
                _logger.LogInformation(logMessage);
            else
                _logger.LogWarning(logMessage);

            // Also log to persistent store for important actions
            await _persistentAuditLogService.LogAsync(action, entity, entityId, details);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for {Action} on {EntityType}", action, entity);
        }
    }

    public async Task LogDocumentCreatedAsync(Guid documentId, string title)
    {
        await LogActionAsync("DOCUMENT_CREATED", "Document", documentId, $"Title: {title}");
    }

    public async Task LogDocumentSubmittedAsync(Guid documentId)
    {
        await LogActionAsync("DOCUMENT_SUBMITTED", "Document", documentId, "Document submitted for approval");
    }

    public async Task LogDocumentApprovedAsync(Guid documentId, string? comments = null)
    {
        await LogActionAsync("DOCUMENT_APPROVED", "Document", documentId, $"Comments: {comments ?? "None"}");
    }

    public async Task LogDocumentRejectedAsync(Guid documentId, string? reason = null)
    {
        await LogActionAsync("DOCUMENT_REJECTED", "Document", documentId, $"Reason: {reason ?? "Not provided"}");
    }

    public async Task LogDocumentEditedAsync(Guid documentId)
    {
        await LogActionAsync("DOCUMENT_EDITED", "Document", documentId, "Document content modified");
    }

    public async Task LogTaskCreatedAsync(Guid taskId, string title, Guid? assignedToId)
    {
        await LogActionAsync("TASK_CREATED", "Task", taskId, 
            $"Title: {title}, Assigned to: {assignedToId}");
    }

    public async Task LogTaskCompletedAsync(Guid taskId)
    {
        await LogActionAsync("TASK_COMPLETED", "Task", taskId, "Task marked as completed");
    }

    public async Task LogTaskRejectedAsync(Guid taskId, string? reason = null)
    {
        await LogActionAsync("TASK_REJECTED", "Task", taskId, $"Reason: {reason ?? "Not provided"}");
    }

    public async Task LogUserLoginAsync(Guid userId)
    {
        await LogActionAsync("USER_LOGIN", "User", userId, $"User logged in");
        await _persistentAuditLogService.LogUserActivityAsync(userId, "Login");
    }

    public async Task LogUserLogoutAsync(Guid userId)
    {
        await LogActionAsync("USER_LOGOUT", "User", userId, $"User logged out");
        await _persistentAuditLogService.LogUserActivityAsync(userId, "Logout");
    }

    public async Task LogFailedLoginAsync(string username)
    {
        _logger.LogWarning("[LOGIN_FAILED] Username: {Username}, IP: {IpAddress}", 
            username, _currentUserService.IpAddress);
        
        await _persistentAuditLogService.LogAuthenticationAsync(
            null, 
            "LOGIN_FAILED", 
            $"Failed login for {username}", 
            _currentUserService.IpAddress, 
            _currentUserService.UserAgent, 
            false);
    }
}

/// <summary>
/// DTO for audit log entries.
/// </summary>
public class AuditLogEntry
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public bool Success { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
