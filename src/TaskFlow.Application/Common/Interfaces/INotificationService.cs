using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null);
    Task SendRealTimeNotificationAsync(Guid userId, string message, NotificationType type);
}