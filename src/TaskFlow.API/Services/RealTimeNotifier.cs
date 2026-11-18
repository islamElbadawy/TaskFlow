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
}
