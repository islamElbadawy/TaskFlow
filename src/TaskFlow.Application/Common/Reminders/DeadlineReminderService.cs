using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Settings;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Common.Reminders;

/// <summary>
/// Creates <see cref="NotificationType.DeadlineApproaching"/> notifications (pushed, and emailed when email is on) for
/// tasks that are due within <see cref="ReminderSettings.WindowHours"/>, not Done, and assigned to an active user.
/// <para>
/// Idempotent: a task's assignee gets one reminder per due date. A reminder counts for the current due date when it was
/// created at or after <c>due date - window</c>, the earliest moment a reminder for that due date can exist. So running
/// again sends nothing new, but moving the due date out of the window and back (or reassigning the task) does.
/// </para>
/// Called by the background job (Infrastructure DeadlineReminderWorker) and by POST /api/reminders/run. Scoped.
/// </summary>
public sealed class DeadlineReminderService
{
    // Runs in one process are serialized, so the job and the endpoint can't both remind the same task at once.
    private static readonly SemaphoreSlim RunLock = new(1, 1);

    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly IOptions<ReminderSettings> _settings;

    public DeadlineReminderService(IUnitOfWork unitOfWork, INotificationService notificationService,
        IOptions<ReminderSettings> settings)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
        _settings = settings;
    }

    /// <summary>Sends the reminders that are due now and returns the ids of the tasks that got one.</summary>
    public async Task<IReadOnlyList<Guid>> SendDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        await RunLock.WaitAsync(cancellationToken);
        try
        {
            return await SendAsync(cancellationToken);
        }
        finally
        {
            RunLock.Release();
        }
    }

    private async Task<IReadOnlyList<Guid>> SendAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var window = TimeSpan.FromHours(_settings.Value.WindowHours);
        var until = now + window;

        var candidates = await _unitOfWork.TasksItems.ListAsync(
            t => t.DueDate != null && t.DueDate > now && t.DueDate <= until
                 && t.Status != TaskItemStatus.Done
                 && t.AssignedToUserId != null && t.AssignedTo!.IsActive,
            t => new { t.Id, t.Title, DueDate = t.DueDate!.Value, AssigneeId = t.AssignedToUserId!.Value },
            cancellationToken);
        if (candidates.Count == 0)
            return [];

        // Every reminder that can count for these due dates was created after now - window (due date > now).
        var taskIds = candidates.Select(c => c.Id).ToList();
        var earliest = now - window;
        var sent = await _unitOfWork.Notifications.ListAsync(
            n => n.Type == NotificationType.DeadlineApproaching && n.RelatedTaskId != null
                 && taskIds.Contains(n.RelatedTaskId.Value) && n.CreatedAt >= earliest,
            n => new { TaskId = n.RelatedTaskId!.Value, n.UserId, n.CreatedAt },
            cancellationToken);

        var toRemind = candidates
            .Where(c => !sent.Any(s => s.TaskId == c.Id && s.UserId == c.AssigneeId && s.CreatedAt >= c.DueDate - window))
            .ToList();
        if (toRemind.Count == 0)
            return [];

        foreach (var task in toRemind)
            await _notificationService.StageAsync(task.AssigneeId, NotificationType.DeadlineApproaching,
                $"\"{task.Title}\" is due {DueIn(task.DueDate - now)}", task.Id, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _notificationService.PublishStagedAsync(cancellationToken);

        return toRemind.Select(t => t.Id).ToList();
    }

    /// <summary>"in 5 hours", "in 1 hour", "in less than an hour" (the seed data says "is due in 3 days").</summary>
    private static string DueIn(TimeSpan remaining)
    {
        var hours = (int)remaining.TotalHours;
        return hours switch
        {
            < 1 => "in less than an hour",
            1 => "in 1 hour",
            _ => $"in {hours} hours"
        };
    }
}
