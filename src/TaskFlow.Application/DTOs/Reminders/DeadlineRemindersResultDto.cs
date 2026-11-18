namespace TaskFlow.Application.DTOs.Reminders;

/// <summary>POST /api/reminders/run: how many reminders were created, and for which tasks.</summary>
public record DeadlineRemindersResultDto(int RemindersCreated, IReadOnlyList<Guid> TaskIds);
