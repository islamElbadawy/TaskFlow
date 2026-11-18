using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

/// <summary>
/// Creates notifications. Handlers stage them in the current unit of work, so they are saved by the same
/// SaveChanges as the change that caused them, then publish them (SignalR push) after the save succeeded.
/// </summary>
public interface INotificationService
{
    /// <summary>Adds a notification to the unit of work without saving it.</summary>
    Task StageAsync(Guid userId, NotificationType type, string message, Guid? relatedTaskId,
        CancellationToken cancellationToken = default);

    /// <summary>Pushes every staged notification to its user. Call only after SaveChanges succeeded.
    /// Push failures are logged, never thrown: the notifications are already stored.</summary>
    Task PublishStagedAsync(CancellationToken cancellationToken = default);

    /// <summary>Stages, saves and publishes a single notification.</summary>
    Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null, CancellationToken cancellationToken = default);
}
