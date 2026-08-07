using TaskFlow.Application.Common.Interfaces;
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

    public async Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            IsRead = false,
            RelatedTaskId = relatedTaskId
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();

        var payload = new { message, type = type.ToString(), timestamp = DateTime.UtcNow };
        await _notifier.SendAsync(userId, payload);
    }

    public Task SendRealTimeNotificationAsync(Guid userId, string message, NotificationType type)
    {
        var payload = new { message, type = type.ToString(), timestamp = DateTime.UtcNow };
        return _notifier.SendAsync(userId, payload);
    }
}
