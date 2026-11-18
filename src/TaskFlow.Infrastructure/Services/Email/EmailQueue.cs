using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Email;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Infrastructure.Services.Email;

/// <summary>
/// The <see cref="IEmailService"/> the application uses (singleton): queues messages in memory and returns at once, so
/// a slow or unreachable SMTP server never delays or fails a request. <see cref="EmailDispatcher"/> sends them.
/// Queued messages are lost if the process stops before they are sent (acceptable for notification emails).
/// </summary>
public sealed class EmailQueue : IEmailService
{
    private const int Capacity = 1000;

    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly IOptions<SmtpSettings> _options;
    private readonly ILogger<EmailQueue> _logger;

    public EmailQueue(IOptions<SmtpSettings> options, ILogger<EmailQueue> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsEnabled => _options.Value.Enabled;

    public ChannelReader<EmailMessage> Reader => _channel.Reader;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return Task.CompletedTask;

        // TryWrite instead of WriteAsync: when the queue is full, drop the email rather than block the request.
        if (!_channel.Writer.TryWrite(message))
            _logger.LogWarning("Email queue is full ({Capacity}); dropped the email \"{Subject}\"", Capacity, message.Subject);

        return Task.CompletedTask;
    }
}
