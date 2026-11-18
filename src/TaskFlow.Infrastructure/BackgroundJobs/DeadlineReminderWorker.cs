using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Reminders;
using TaskFlow.Application.Common.Settings;

namespace TaskFlow.Infrastructure.BackgroundJobs;

/// <summary>
/// Runs <see cref="DeadlineReminderService"/> at start and then every <see cref="ReminderSettings.IntervalMinutes"/>.
/// Each run gets its own DI scope (the service, the unit of work and the DbContext are scoped). A failed run is logged
/// and the next one runs as planned. Does nothing when <see cref="ReminderSettings.Enabled"/> is false.
/// </summary>
public sealed class DeadlineReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ReminderSettings> _settings;
    private readonly ILogger<DeadlineReminderWorker> _logger;

    public DeadlineReminderWorker(IServiceScopeFactory scopeFactory, IOptions<ReminderSettings> settings,
        ILogger<DeadlineReminderWorker> logger)
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
            _logger.LogInformation("Deadline reminders are disabled (Reminders:Enabled is false)");
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
            var reminders = scope.ServiceProvider.GetRequiredService<DeadlineReminderService>();
            var taskIds = await reminders.SendDueRemindersAsync(stoppingToken);
            if (taskIds.Count > 0)
                _logger.LogInformation("Sent {Count} deadline reminder(s)", taskIds.Count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The deadline reminder run failed");
        }
    }
}
