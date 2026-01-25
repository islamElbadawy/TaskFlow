using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(int userId, string message, NotificationType type, int? relatedTaskId = null);
    Task SendRealTimeNotificationAsync(int userId, string message, NotificationType type);
}