using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Settings;

namespace TaskFlow.Infrastructure.BackgroundJobs;

/// <summary>
/// Polls <see cref="IOutboxProcessor"/> at start and then every <see cref="OutboxSettings.IntervalMinutes"/> (default 10 min)
/// to retry due Email outbox messages with exponential backoff and purge old Sent rows.
/// Does nothing when <see cref="OutboxSettings.Enabled"/> is false.
/// </summary>
public sealed class OutboxProcessorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OutboxSettings> _settings;
    private readonly ILogger<OutboxProcessorWorker> _logger;

    public OutboxProcessorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxSettings> settings,
        ILogger<OutboxProcessorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _settings.Value;
        if (!settings.Enabled)
        {
            _logger.LogInformation("Outbox background worker is disabled (Outbox:Enabled is false)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(settings.IntervalMinutes));
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

            var sent = await processor.ProcessDueEmailsAsync(stoppingToken);
            if (sent > 0)
                _logger.LogInformation("Outbox worker delivered {Count} email(s)", sent);

            var purged = await processor.CleanupSentMessagesAsync(stoppingToken);
            if (purged > 0)
                _logger.LogInformation("Outbox worker purged {Count} old sent outbox row(s)", purged);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The outbox processor run failed");
        }
    }
}
