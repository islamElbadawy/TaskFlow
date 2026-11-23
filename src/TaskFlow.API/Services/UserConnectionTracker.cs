using System.Collections.Concurrent;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.API.Services;

/// <summary>
/// Thread-safe singleton tracking active SignalR hub connection counts per user.
/// Supports multiple concurrent tabs/sessions per user without leaking zero-count entries.
/// </summary>
public sealed class UserConnectionTracker : IUserConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    public void OnConnected(Guid userId)
    {
        _connections.AddOrUpdate(userId, 1, static (_, count) => count + 1);
    }

    public void OnDisconnected(Guid userId)
    {
        while (true)
        {
            if (!_connections.TryGetValue(userId, out var current))
                return;

            if (current <= 1)
            {
                if (_connections.TryRemove(new KeyValuePair<Guid, int>(userId, current)))
                    return;
            }
            else if (_connections.TryUpdate(userId, current - 1, current))
            {
                return;
            }
        }
    }

    public bool IsOnline(Guid userId) =>
        _connections.TryGetValue(userId, out var count) && count > 0;
}
