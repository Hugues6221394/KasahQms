using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KasahQMS.Web.Services;

/// <summary>
/// Implementation of real-time notification service using SignalR.
/// </summary>
public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<NotificationsHub> _hubContext;

    public RealTimeNotificationService(IHubContext<NotificationsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyTaskAssignedAsync(Guid recipientUserId, Guid taskId, string taskTitle, string assignerName, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"user:{recipientUserId}").SendAsync("TaskAssigned", new
        {
            TaskId = taskId,
            Title = taskTitle,
            AssignerName = assignerName,
            Time = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task NotifyTaskUpdateAsync(Guid recipientUserId, Guid taskId, string taskTitle, string assigneeName, string updateMessage, int? progressPercentage = null, CancellationToken cancellationToken = default)
    {
        var title = progressPercentage.HasValue 
            ? $"Task Progress Update: {progressPercentage}%"
            : "Task Update Received";
        
        var message = $"{assigneeName} posted an update on \"{taskTitle}\": {(updateMessage.Length > 80 ? updateMessage.Substring(0, 77) + "..." : updateMessage)}";
        
        await _hubContext.Clients.Group($"user:{recipientUserId}").SendAsync("TaskUpdate", new
        {
            TaskId = taskId,
            Title = title,
            Message = message,
            AssigneeName = assigneeName,
            ProgressPercentage = progressPercentage,
            Time = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task NotifyDocumentReceivedAsync(Guid recipientUserId, Guid documentId, string documentTitle, string senderName, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"user:{recipientUserId}").SendAsync("DocumentReceived", new
        {
            DocumentId = documentId,
            Title = documentTitle,
            SenderName = senderName,
            Time = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task NotifyNewMessageAsync(Guid recipientUserId, Guid threadId, string senderName, string messagePreview, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"user:{recipientUserId}").SendAsync("NewMessage", new
        {
            ThreadId = threadId,
            SenderName = senderName,
            Content = messagePreview?.Length > 50 ? messagePreview.Substring(0, 47) + "..." : messagePreview,
            Time = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task RefreshBadgesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"user:{userId}").SendAsync("RefreshBadges", cancellationToken);
    }

    public async Task SendNotificationAsync(Guid recipientUserId, string title, string message, string type, Guid? relatedEntityId = null, string? relatedEntityType = null, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group($"user:{recipientUserId}").SendAsync("NotificationReceived", new
        {
            Title = title,
            Message = message,
            Type = type,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            Time = DateTime.UtcNow
        }, cancellationToken);
    }
}
