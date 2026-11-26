using System.Threading.Channels;

namespace TaskFlow.Application.Common.Interfaces;

/// <summary>
/// Lightweight in-process wake-up signal so <c>NotificationService</c> can nudge <c>OutboxProcessorWorker</c>
/// to send newly staged emails right away without blocking the HTTP request on SMTP.
/// </summary>
public sealed class OutboxSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void Notify() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
