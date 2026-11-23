using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Common.Settings;

/// <summary>The <c>Outbox</c> configuration section (transactional outbox worker and retry policy), validated on start.</summary>
public sealed class OutboxSettings
{
    public const string SectionName = "Outbox";

    /// <summary>Runs the background outbox worker. When false, due messages are only processed via immediate send or explicit calls.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the background worker polls for due email messages and cleans up old sent rows.</summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 10;

    /// <summary>Maximum number of due outbox rows claimed per batch.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    /// <summary>Maximum delivery attempts before a message transitions to DeadLetter.</summary>
    [Range(1, 50)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Base delay for exponential backoff retries (e.g. 10m -> 20m -> 40m -> 80m).</summary>
    [Range(1, 1440)]
    public int BaseRetryDelayMinutes { get; set; } = 10;

    /// <summary>Number of days to retain Sent outbox rows before purging.</summary>
    [Range(1, 365)]
    public int RetentionDays { get; set; } = 7;
}
