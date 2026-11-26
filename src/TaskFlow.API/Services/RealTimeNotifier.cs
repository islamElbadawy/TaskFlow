using Microsoft.AspNetCore.SignalR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.DTOs;
using TaskFlow.API.Hubs;

namespace TaskFlow.API.Services;

/// <summary>Pushes notifications to every open connection of a user, on any device.</summary>
public class RealTimeNotifier : IRealTimeNotifier
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

    public RealTimeNotifier(IHubContext<NotificationHub, INotificationClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task SendAsync(Guid userId, NotificationDto notification) =>
        _hubContext.Clients.User(userId.ToString()).ReceiveNotification(notification);

    public async Task NotifyBoardChangedAsync(BoardChangeDto change, IEnumerable<Guid>? targetUserIds = null)
    {
        if (targetUserIds is null)
        {
            await _hubContext.Clients.All.BoardChanged(change);
            return;
        }

        var recipients = targetUserIds
            .Where(id => id != Guid.Empty && id != change.ActorId)
            .Select(id => id.ToString())
            .Distinct()
            .ToList();

        await _hubContext.Clients.Group(NotificationHub.AdminGroup).BoardChanged(change);
        if (recipients.Count > 0)
            await _hubContext.Clients.Users(recipients).BoardChanged(change);
    }
}
