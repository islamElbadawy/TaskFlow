using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>PATCH /api/tasks/{id}/status, body <c>{ "status": "InProgress" }</c>.</summary>
public class ChangeTaskStatusCommand : ICommand<Result<TaskDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    public Guid TaskId { get; set; }

    /// <summary>Todo, InProgress or Done (case-insensitive).</summary>
    public string Status { get; set; } = string.Empty;
}
