namespace TaskFlow.Application.Common.Interfaces;

public interface IRealTimeNotifier
{
    Task SendAsync(Guid userId, object payload);
}
