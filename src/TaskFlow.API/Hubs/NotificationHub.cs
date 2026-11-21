using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Application.DTOs;

namespace TaskFlow.API.Hubs;

/// <summary>Methods the server calls on connected clients.</summary>
public interface INotificationClient
{
    /// <summary>A new notification for the connected user (same shape as GET /api/notifications items).</summary>
    Task ReceiveNotification(NotificationDto notification);

    /// <summary>Broadcast to connected clients when any task is created, updated, moved, or deleted.</summary>
    Task BoardChanged(BoardChangeDto change);
}

/// <summary>
/// Real-time channel at <c>/hubs/notifications</c>. Clients connect with their JWT
/// (<c>?access_token=...</c>, see Program.cs) and receive <c>ReceiveNotification</c>. No connection bookkeeping
/// is needed: SignalR groups connections by user via the default IUserIdProvider (ClaimTypes.NameIdentifier),
/// and the server sends with <c>Clients.User(userId)</c>.
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
}
