using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>PATCH /api/tasks/{id}/assignee, body <c>{ "assignedToId": "&lt;guid&gt;" }</c>, or null to unassign.</summary>
public class AssignTaskCommand : ICommand<Result<TaskDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The new assignee (an active user), or null to unassign.</summary>
    public Guid? AssignedToId { get; set; }
}
