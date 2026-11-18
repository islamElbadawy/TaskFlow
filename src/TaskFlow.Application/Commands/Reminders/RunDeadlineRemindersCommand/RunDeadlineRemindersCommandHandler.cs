using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Reminders;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs.Reminders;

namespace TaskFlow.Application.Commands.Reminders.RunDeadlineRemindersCommand;

/// <summary>Same run as the background job (idempotent, so running it early never sends a reminder twice).
/// The Admin-only rule is on the endpoint.</summary>
public class RunDeadlineRemindersCommandHandler
    : ICommandHandler<RunDeadlineRemindersCommand, Result<DeadlineRemindersResultDto>>
{
    private readonly DeadlineReminderService _reminders;

    public RunDeadlineRemindersCommandHandler(DeadlineReminderService reminders)
    {
        _reminders = reminders;
    }

    public async Task<Result<DeadlineRemindersResultDto>> HandleAsync(RunDeadlineRemindersCommand command,
        CancellationToken cancellationToken = default)
    {
        var taskIds = await _reminders.SendDueRemindersAsync(cancellationToken);
        return Result<DeadlineRemindersResultDto>.Success(new DeadlineRemindersResultDto(taskIds.Count, taskIds));
    }
}
