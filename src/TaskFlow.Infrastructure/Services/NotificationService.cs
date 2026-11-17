using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Notifications;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealTimeNotifier _notifier;

    public NotificationService(IUnitOfWork unitOfWork, IRealTimeNotifier notifier)
    {
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null, CancellationToken cancellationToken = default)
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
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _notifier.SendAsync(userId, NotificationProjections.ToDtoFromEntity(notification));
    }
}
