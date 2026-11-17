using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Notifications;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Services;

/// <summary>Scoped (one per request): remembers what was staged in this request until it is published.</summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealTimeNotifier _notifier;
    private readonly ILogger<NotificationService> _logger;
    private readonly List<Notification> _staged = new();

    public NotificationService(IUnitOfWork unitOfWork, IRealTimeNotifier notifier, ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task StageAsync(Guid userId, NotificationType type, string message, Guid? relatedTaskId,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            IsRead = false,
            RelatedTaskId = relatedTaskId
        };

        await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
        _staged.Add(notification);
    }

    public async Task PublishStagedAsync(CancellationToken cancellationToken = default)
    {
        var toPublish = _staged.ToList();
        _staged.Clear();

        foreach (var notification in toPublish)
        {
            try
            {
                await _notifier.SendAsync(notification.UserId, NotificationProjections.ToDtoFromEntity(notification));
            }
            catch (Exception ex)
            {
                // The notification is stored; the client gets it from GET /api/notifications on its next fetch.
                _logger.LogWarning(ex, "Real-time push of notification {NotificationId} to user {UserId} failed",
                    notification.Id, notification.UserId);
            }
        }
    }

    public async Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null, CancellationToken cancellationToken = default)
    {
        await StageAsync(userId, type, message, relatedTaskId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishStagedAsync(cancellationToken);
    }
}
