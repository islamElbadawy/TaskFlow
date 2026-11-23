using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public class OutboxMessage : BaseEntity
{
    public const int MaxErrorLength = 2000;

    public OutboxChannel Channel { get; set; }
    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
    public Guid? RecipientUserId { get; set; }
    public Guid? NotificationId { get; set; }
    public string PayloadJson { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime NextAttemptAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }

    public void MarkSent(DateTime utcNow)
    {
        Status = OutboxStatus.Sent;
        ProcessedAt = utcNow;
        LastError = null;
    }

    public void RecordFailure(string error, DateTime utcNow, TimeSpan baseDelay)
    {
        AttemptCount++;
        LastError = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;

        if (AttemptCount >= MaxAttempts)
        {
            Status = OutboxStatus.DeadLetter;
            return;
        }

        Status = OutboxStatus.Pending;
        var backoffFactor = Math.Pow(2, Math.Max(0, AttemptCount - 1));
        NextAttemptAt = utcNow + TimeSpan.FromTicks((long)(baseDelay.Ticks * backoffFactor));
    }
}
