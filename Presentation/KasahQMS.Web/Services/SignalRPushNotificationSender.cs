using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace KasahQMS.Web.Services;

/// <summary>
/// Implementation of IPushNotificationSender using SignalR.
/// Sends real-time push notifications via NotificationsHub.
/// </summary>
public class SignalRPushNotificationSender : IPushNotificationSender
{
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly ILogger<SignalRPushNotificationSender> _logger;

    public SignalRPushNotificationSender(
        IHubContext<NotificationsHub> hubContext,
        ILogger<SignalRPushNotificationSender> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendToUserAsync(
        Guid userId,
        string eventName,
        object payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = $"user:{userId}";
            await _hubContext.Clients.Group(groupName).SendAsync(eventName, payload, cancellationToken);
            _logger.LogDebug("Push notification sent to user {UserId}: {EventName}", userId, eventName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send push notification to user {UserId}", userId);
        }
    }
}
