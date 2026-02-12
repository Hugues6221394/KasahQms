using KasahQMS.Application.Common.Interfaces.Services;
using KasahQMS.Domain.Entities.Notifications;
using KasahQMS.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KasahQMS.Infrastructure.Persistence.Services;

/// <summary>
/// Notification service implementation.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPushNotificationSender _pushSender;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext dbContext,
        IPushNotificationSender pushSender,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task SendAsync(
        Guid userId,
        string title,
        string message,
        NotificationType type,
        Guid? relatedEntityId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var notification = Notification.Create(
                userId,
                title,
                message,
                type,
                relatedEntityId,
                type.ToString());

            _dbContext.Set<Notification>().Add(notification);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var payload = new
            {
                notification.Id,
                Title = title,
                Message = message,
                Type = type.ToString(),
                notification.RelatedEntityId,
                notification.RelatedEntityType
            };
            await _pushSender.SendToUserAsync(userId, "Notification", payload, cancellationToken);

            _logger.LogDebug("Notification sent to user {UserId}: {Title}", userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Set<Notification>()
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification != null)
        {
            notification.MarkAsRead();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Notification>()
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }
}

