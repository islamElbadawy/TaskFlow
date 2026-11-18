using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Application.Common.Settings;

/// <summary>The <c>Reminders</c> configuration section (deadline reminders), validated on start.</summary>
public sealed class ReminderSettings
{
    public const string SectionName = "Reminders";

    /// <summary>Runs the background job. When false, reminders are only created through POST /api/reminders/run.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the background job looks for tasks that are due soon (it also runs once at start).</summary>
    [Range(1, 1440)]
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>A task gets a reminder once it is due within this many hours.</summary>
    [Range(1, 168)]
    public int WindowHours { get; set; } = 24;
}
