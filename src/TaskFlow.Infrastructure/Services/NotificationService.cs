using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Email;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Settings;
using TaskFlow.Application.Queries.Notifications;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Services;

/// <summary>
/// Scoped (one per request): stages each <see cref="Notification"/> alongside its <see cref="OutboxMessage"/> rows
/// (<see cref="OutboxChannel.Push"/>, plus <see cref="OutboxChannel.Email"/> for emailed notification types) so all
/// commit atomically in the caller's <see cref="IUnitOfWork.SaveChangesAsync"/> transaction.
/// Publishing delivers Push immediately to online users (leaving offline users Pending for session-open flush)
/// and attempts best-effort immediate Email delivery via <see cref="IOutboxProcessor"/>.
/// </summary>
public class NotificationService : INotificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealTimeNotifier _notifier;
    private readonly IUserConnectionTracker _connectionTracker;
    private readonly IOutboxProcessor _outboxProcessor;
    private readonly IOptions<OutboxSettings> _outboxSettings;
    private readonly ILogger<NotificationService> _logger;
    private readonly List<StagedEntry> _staged = new();

    public NotificationService(
        IUnitOfWork unitOfWork,
        IRealTimeNotifier notifier,
        IUserConnectionTracker connectionTracker,
        IOutboxProcessor outboxProcessor,
        IOptions<OutboxSettings> outboxSettings,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _connectionTracker = connectionTracker;
        _outboxProcessor = outboxProcessor;
        _outboxSettings = outboxSettings;
        _logger = logger;
    }

    public async Task StageAsync(Guid userId, NotificationType type, string message, Guid? relatedTaskId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            UserId = userId,
            Message = message,
            Type = type,
            IsRead = false,
            RelatedTaskId = relatedTaskId,
            CreatedAt = now
        };

        await _unitOfWork.Notifications.AddAsync(notification, cancellationToken);

        var maxAttempts = _outboxSettings.Value.MaxAttempts;
        var pushOutbox = new OutboxMessage
        {
            Channel = OutboxChannel.Push,
            Status = OutboxStatus.Pending,
            RecipientUserId = userId,
            NotificationId = notification.Id,
            PayloadJson = JsonSerializer.Serialize(NotificationProjections.ToDtoFromEntity(notification), JsonOptions),
            MaxAttempts = maxAttempts,
            NextAttemptAt = now,
            CreatedAt = now
        };

        await _unitOfWork.OutboxMessages.AddAsync(pushOutbox, cancellationToken);

        OutboxMessage? emailOutbox = null;
        if (NotificationEmails.IsEmailed(type))
        {
            var recipient = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
            if (recipient is { IsActive: true })
            {
                var task = relatedTaskId is { } taskId
                    ? await _unitOfWork.TasksItems.GetByIdAsync(taskId, cancellationToken)
                    : null;

                var emailMessage = NotificationEmails.Create(
                    type,
                    recipient.Email,
                    $"{recipient.FirstName} {recipient.LastName}",
                    message,
                    task?.Title,
                    task?.DueDate);

                emailOutbox = new OutboxMessage
                {
                    Channel = OutboxChannel.Email,
                    Status = OutboxStatus.Pending,
                    RecipientUserId = userId,
                    NotificationId = notification.Id,
                    PayloadJson = JsonSerializer.Serialize(emailMessage, JsonOptions),
                    MaxAttempts = maxAttempts,
                    NextAttemptAt = now,
                    CreatedAt = now
                };

                await _unitOfWork.OutboxMessages.AddAsync(emailOutbox, cancellationToken);
            }
        }

        _staged.Add(new StagedEntry(notification, pushOutbox, emailOutbox));
    }

    public async Task PublishStagedAsync(CancellationToken cancellationToken = default)
    {
        var toPublish = _staged.ToList();
        _staged.Clear();

        var pushUpdated = false;
        foreach (var entry in toPublish)
        {
            if (!_connectionTracker.IsOnline(entry.Notification.UserId))
                continue;

            try
            {
                await _notifier.SendAsync(entry.Notification.UserId,
                    NotificationProjections.ToDtoFromEntity(entry.Notification));
                entry.PushOutbox.MarkSent(DateTime.UtcNow);
                pushUpdated = true;
            }
            catch (Exception ex)
            {
                // Leave Pending so NotificationHub.OnConnectedAsync flushes it on the next open session.
                _logger.LogWarning(ex,
                    "Real-time push of notification {NotificationId} to user {UserId} failed; leaving outbox {OutboxMessageId} Pending",
                    entry.Notification.Id, entry.Notification.UserId, entry.PushOutbox.Id);
            }
        }

        if (pushUpdated)
        {
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to update delivered push outbox row(s) to Sent");
            }
        }

        foreach (var entry in toPublish)
        {
            if (entry.EmailOutbox is null)
                continue;

            try
            {
                await _outboxProcessor.TrySendEmailByIdAsync(entry.EmailOutbox.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Leave Pending for the scheduled OutboxProcessorWorker retry loop.
                _logger.LogWarning(ex, "Immediate email delivery for outbox {OutboxMessageId} failed",
                    entry.EmailOutbox.Id);
            }
        }
    }

    public async Task CreateNotificationAsync(Guid userId, string message, NotificationType type, Guid? relatedTaskId = null, CancellationToken cancellationToken = default)
    {
        await StageAsync(userId, type, message, relatedTaskId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await PublishStagedAsync(cancellationToken);
    }

    private sealed record StagedEntry(Notification Notification, OutboxMessage PushOutbox, OutboxMessage? EmailOutbox);
}
