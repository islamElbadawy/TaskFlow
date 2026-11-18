using Microsoft.Extensions.Logging;
using TaskFlow.Application.Common.Email;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Notifications;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Services;

/// <summary>
/// Scoped (one per request): remembers what was staged in this request until it is published. Publishing pushes each
/// notification over SignalR and emails the types listed in <see cref="NotificationEmails"/> to active recipients.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealTimeNotifier _notifier;
    private readonly IEmailService _emailService;
    private readonly ILogger<NotificationService> _logger;
    private readonly List<Notification> _staged = new();

    public NotificationService(IUnitOfWork unitOfWork, IRealTimeNotifier notifier, IEmailService emailService,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task StageAsync(Guid userId, NotificationType type, string message, Guid? relatedTaskId,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            IsRead = false,
            RelatedTaskId = relatedTaskId
        };

        await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);
        _staged.Add(notification);
    }

    public async Task PublishStagedAsync(CancellationToken cancellationToken = default)
    {
        var toPublish = _staged.ToList();
        _staged.Clear();

        foreach (var notification in toPublish)
        {
            try
            {
                await _notifier.SendAsync(notification.UserId, NotificationProjections.ToDtoFromEntity(notification));
            }
            catch (Exception ex)
            {
                // The notification is stored; the client gets it from GET /api/notifications on its next fetch.
                _logger.LogWarning(ex, "Real-time push of notification {NotificationId} to user {UserId} failed",
                    notification.Id, notification.UserId);
            }
        }

        await EmailAsync(toPublish, cancellationToken);
    }

    /// <summary>Emails the notifications whose type is emailed. Failures are logged: the notifications are stored.</summary>
    private async Task EmailAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
    {
        if (!_emailService.IsEnabled)
            return;

        var emailed = notifications.Where(n => NotificationEmails.IsEmailed(n.Type)).ToList();
        if (emailed.Count == 0)
            return;

        try
        {
            var userIds = emailed.Select(n => n.UserId).Distinct().ToList();
            var recipients = (await _unitOfWork.Users.ListAsync(u => userIds.Contains(u.Id) && u.IsActive,
                    u => new { u.Id, u.Email, Name = u.FirstName + " " + u.LastName }, cancellationToken))
                .ToDictionary(u => u.Id);

            var taskIds = emailed.Where(n => n.RelatedTaskId != null).Select(n => n.RelatedTaskId!.Value).Distinct().ToList();
            var tasks = (await _unitOfWork.TasksItems.ListAsync(t => taskIds.Contains(t.Id),
                    t => new { t.Id, t.Title, t.DueDate }, cancellationToken))
                .ToDictionary(t => t.Id);

            foreach (var notification in emailed)
            {
                if (!recipients.TryGetValue(notification.UserId, out var recipient))
                    continue;

                var task = notification.RelatedTaskId is { } taskId ? tasks.GetValueOrDefault(taskId) : null;
                await _emailService.SendAsync(NotificationEmails.Create(notification.Type, recipient.Email,
                    recipient.Name, notification.Message, task?.Title, task?.DueDate), cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Emailing {Count} notification(s) failed", emailed.Count);
        }
    }

    public async Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null, CancellationToken cancellationToken = default)
    {
        await StageAsync(userId, type, message, relatedTaskId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishStagedAsync(cancellationToken);
    }
}
