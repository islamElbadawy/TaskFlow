using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TaskFlow.Application.Common.Interfaces;
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
/// (<c>?access_token=...</c>, see Program.cs) and receive <c>ReceiveNotification</c>.
/// Tracks active connections via <see cref="IUserConnectionTracker"/> and flushes any pending Push
/// outbox notifications for the connecting user on every new open session.
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public const string AdminGroup = "admins";

    private readonly IUserConnectionTracker _connectionTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IUserConnectionTracker connectionTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationHub> logger)
    {
        _connectionTracker = connectionTracker;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();

        if (!Guid.TryParse(Context.UserIdentifier, out var userId))
            return;

        _connectionTracker.OnConnected(userId);
        if (Context.User?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            await outbox.FlushPendingPushForUserAsync(userId, Context.ConnectionAborted);
        }
        catch (OperationCanceledException) when (Context.ConnectionAborted.IsCancellationRequested)
        {
            // Client disconnected during connect flush; rows stay Pending.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush pending push notifications on connect for user {UserId}", userId);
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            _connectionTracker.OnDisconnected(userId);

        await base.OnDisconnectedAsync(exception);
    }
}
