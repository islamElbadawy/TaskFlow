using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Common.Notifications;

/// <summary>
/// Who gets notified about task and comment events, in one place. Rules:
/// <list type="bullet">
/// <item>task assigned → the new assignee (TaskAssigned)</item>
/// <item>status changed to Done → the creator (TaskCompleted)</item>
/// <item>task updated (fields or status) → the assignee (TaskUpdated)</item>
/// <item>comment added → the creator and the assignee (CommentAdded)</item>
/// </list>
/// The actor is never notified, and each user gets at most one notification per task per request (the first
/// rule that applies wins, so call the methods in the order above). Notifications are only staged here; the
/// handler saves them with its change and then calls <see cref="PublishAsync"/>.
/// Scoped: one instance per request.
/// </summary>
public sealed class TaskNotifications
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;
    private readonly HashSet<(Guid UserId, Guid TaskId)> _notified = new();
    private string? _actorName;

    public TaskNotifications(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public Task TaskAssignedAsync(Guid actorId, Guid taskId, string title, Guid? assigneeId, CancellationToken cancellationToken) =>
        NotifyAsync(assigneeId, actorId, taskId, NotificationType.TaskAssigned,
            actor => $"{actor} assigned you \"{title}\"", cancellationToken);

    public Task TaskCompletedAsync(Guid actorId, Guid taskId, string title, Guid creatorId, CancellationToken cancellationToken) =>
        NotifyAsync(creatorId, actorId, taskId, NotificationType.TaskCompleted,
            actor => $"{actor} marked \"{title}\" as done", cancellationToken);

    public Task TaskUpdatedAsync(Guid actorId, Guid taskId, string title, Guid? assigneeId, CancellationToken cancellationToken) =>
        NotifyAsync(assigneeId, actorId, taskId, NotificationType.TaskUpdated,
            actor => $"{actor} updated \"{title}\"", cancellationToken);

    public Task StatusChangedAsync(Guid actorId, Guid taskId, string title, Guid? assigneeId, TaskItemStatus status,
        CancellationToken cancellationToken) =>
        NotifyAsync(assigneeId, actorId, taskId, NotificationType.TaskUpdated,
            actor => $"{actor} moved \"{title}\" to {status}", cancellationToken);

    public async Task CommentAddedAsync(Guid actorId, Guid taskId, string title, Guid creatorId, Guid? assigneeId,
        CancellationToken cancellationToken)
    {
        foreach (var recipient in new[] { creatorId, assigneeId })
            await NotifyAsync(recipient, actorId, taskId, NotificationType.CommentAdded,
                actor => $"{actor} commented on \"{title}\"", cancellationToken);
    }

    /// <summary>Pushes what was staged. Call after SaveChanges succeeded.</summary>
    public Task PublishAsync(CancellationToken cancellationToken) =>
        _notificationService.PublishStagedAsync(cancellationToken);

    private async Task NotifyAsync(Guid? recipientId, Guid actorId, Guid taskId, NotificationType type,
        Func<string, string> message, CancellationToken cancellationToken)
    {
        if (recipientId is not { } recipient || recipient == actorId || !_notified.Add((recipient, taskId)))
            return;

        _actorName ??= (await _unitOfWork.Users.ListAsync(u => u.Id == actorId,
            u => u.FirstName + " " + u.LastName, cancellationToken)).SingleOrDefault() ?? "Someone";

        await _notificationService.StageAsync(recipient, type, message(_actorName), taskId, cancellationToken);
    }
}
