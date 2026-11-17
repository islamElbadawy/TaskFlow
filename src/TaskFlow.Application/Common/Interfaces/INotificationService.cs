using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface INotificationService
{
    /// <summary>Saves a notification on its own and pushes it to the user.</summary>
    Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null, CancellationToken cancellationToken = default);
}
