namespace TaskFlow.Application.Common.Interfaces;

/// <summary>
/// Tracks active real-time hub connections per user (supports multiple open tabs/sessions per user).
/// </summary>
public interface IUserConnectionTracker
{
    void OnConnected(Guid userId);
    void OnDisconnected(Guid userId);
    bool IsOnline(Guid userId);
}
