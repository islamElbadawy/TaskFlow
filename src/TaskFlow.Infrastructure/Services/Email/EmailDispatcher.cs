using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Infrastructure.Services.Email;

/// <summary>Background service that sends the queued emails one by one. A failed email is logged and skipped.</summary>
public sealed class EmailDispatcher : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly ILogger<EmailDispatcher> _logger;

    public EmailDispatcher(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger)
    {
        _queue = queue;
        _sender = sender;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _sender.SendAsync(message, stoppingToken);
                _logger.LogInformation("Sent the email \"{Subject}\"", message.Subject);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The recipient's address is personal data: log the subject only.
                _logger.LogError(ex, "Sending the email \"{Subject}\" failed", message.Subject);
            }
        }
    }
}
