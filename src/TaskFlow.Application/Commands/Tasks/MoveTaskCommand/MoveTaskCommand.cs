using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>PATCH /api/tasks/{id}/move, body <c>{ "status": "Done", "beforeTaskId": "&lt;guid&gt;" | null }</c>.</summary>
public class MoveTaskCommand : ICommand<Result<TaskDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The target column: Todo, InProgress or Done (case-insensitive). May be the current one (reorder).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Place the task directly above this task (which must be in the target column); null = at the end.</summary>
    public Guid? BeforeTaskId { get; set; }
}
