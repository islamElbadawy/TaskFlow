using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Common.Interfaces;

/// <summary>Port for live delivery (implemented with SignalR in the API layer).</summary>
public interface IRealTimeNotifier
{
    Task SendAsync(Guid userId, NotificationDto notification);
    Task NotifyBoardChangedAsync(BoardChangeDto change, IEnumerable<Guid>? targetUserIds = null);
}
