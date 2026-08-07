using Microsoft.AspNetCore.SignalR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.API.Hubs;

namespace TaskFlow.API.Services;

public class RealTimeNotifier : IRealTimeNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public RealTimeNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendAsync(Guid userId, object payload)
    {
        var connectionIds = NotificationHub.GetConnectionsForUser(userId);
        foreach (var connectionId in connectionIds)
            await _hubContext.Clients.Client(connectionId).SendAsync("ReceiveNotification", payload);
    }
}
