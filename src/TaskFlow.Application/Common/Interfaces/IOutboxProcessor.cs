namespace TaskFlow.Application.Common.Interfaces;

/// <summary>
/// Processes persisted outbox messages for both Email (with exponential backoff retry) and Push (session-open flush).
/// </summary>
public interface IOutboxProcessor
{
    /// <summary>Claims and delivers due Email outbox messages in batches. Returns how many were marked Sent.</summary>
    Task<int> ProcessDueEmailsAsync(CancellationToken cancellationToken = default);

    /// <summary>Best-effort immediate delivery for one newly staged Email outbox row. Returns true if marked Sent.</summary>
    Task<bool> TrySendEmailByIdAsync(Guid outboxMessageId, CancellationToken cancellationToken = default);

    /// <summary>Flushes all Pending Push outbox messages for a user on session open (skips deactivated users). Returns how many were marked Sent.</summary>
    Task<int> FlushPendingPushForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Deletes Sent outbox rows older than <c>Outbox:RetentionDays</c>. Returns how many rows were deleted.</summary>
    Task<int> CleanupSentMessagesAsync(CancellationToken cancellationToken = default);
}
