namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for sending real-time notifications via SignalR.
/// </summary>
public interface IRealTimeNotificationService
{
    /// <summary>Send notification when a task is assigned to a user.</summary>
    Task NotifyTaskAssignedAsync(Guid recipientUserId, Guid taskId, string taskTitle, string assignerName, CancellationToken cancellationToken = default);
    
    /// <summary>Send notification when a task update is posted (to task creator/assigner).</summary>
    Task NotifyTaskUpdateAsync(Guid recipientUserId, Guid taskId, string taskTitle, string assigneeName, string updateMessage, int? progressPercentage = null, CancellationToken cancellationToken = default);
    
    /// <summary>Send notification when a document is sent to a user.</summary>
    Task NotifyDocumentReceivedAsync(Guid recipientUserId, Guid documentId, string documentTitle, string senderName, CancellationToken cancellationToken = default);
    
    /// <summary>Send notification when a new message is received.</summary>
    Task NotifyNewMessageAsync(Guid recipientUserId, Guid threadId, string senderName, string messagePreview, CancellationToken cancellationToken = default);
    
    /// <summary>Trigger badge refresh for a user.</summary>
    Task RefreshBadgesAsync(Guid userId, CancellationToken cancellationToken = default);
    
    /// <summary>Send generic notification to a user.</summary>
    Task SendNotificationAsync(Guid recipientUserId, string title, string message, string type, Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken cancellationToken = default);
}
