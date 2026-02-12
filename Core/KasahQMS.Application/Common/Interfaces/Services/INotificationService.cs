using KasahQMS.Domain.Entities.Notifications;

namespace KasahQMS.Application.Common.Interfaces.Services;

/// <summary>
/// Service for sending notifications.
/// </summary>
public interface INotificationService
{
    Task SendAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
