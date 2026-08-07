using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private static readonly ConcurrentDictionary<Guid, List<string>> _userConnections = new();

    public override Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            _userConnections.AddOrUpdate(userId.Value,
                new List<string> { Context.ConnectionId },
                (_, existing) => { existing.Add(Context.ConnectionId); return existing; });
        }
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue && _userConnections.TryGetValue(userId.Value, out var connections))
        {
            connections.Remove(Context.ConnectionId);
            if (connections.Count == 0)
                _userConnections.TryRemove(userId.Value, out _);
        }
        return base.OnDisconnectedAsync(exception);
    }

    public static IReadOnlyList<string> GetConnectionsForUser(Guid userId) =>
        _userConnections.TryGetValue(userId, out var connections) ? connections : Array.Empty<string>();

    private Guid? GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
