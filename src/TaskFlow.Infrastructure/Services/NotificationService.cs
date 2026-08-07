using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;

    public NotificationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        // Fire-and-forget real-time push placeholder
        await SendRealTimeNotificationAsync(userId, message, type);
    }

    public Task SendRealTimeNotificationAsync(Guid userId, string message, NotificationType type)
    {
        // Real-time hub will be wired in Feature 4
        return Task.CompletedTask;
    }
}
