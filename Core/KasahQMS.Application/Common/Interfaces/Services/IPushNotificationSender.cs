namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Sends real-time push notifications via SignalR to specific users.
/// Implemented by the Web layer using NotificationsHub.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Sends a push event to a user's SignalR group (user:{userId}).
    /// </summary>
    Task SendToUserAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default);
}
