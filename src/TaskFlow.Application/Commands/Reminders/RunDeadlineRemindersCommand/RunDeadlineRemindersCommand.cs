using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs.Reminders;

namespace TaskFlow.Application.Commands.Reminders.RunDeadlineRemindersCommand;

/// <summary>POST /api/reminders/run (Admin): runs the deadline reminders now instead of waiting for the background job.</summary>
public class RunDeadlineRemindersCommand : ICommand<Result<DeadlineRemindersResultDto>>
{
}
