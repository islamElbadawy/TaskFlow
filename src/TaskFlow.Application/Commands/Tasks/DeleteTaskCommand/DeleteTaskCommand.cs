using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>DELETE /api/tasks/{id}.</summary>
public class DeleteTaskCommand : ICommand<Result<bool>>
{
    public Guid TaskId { get; set; }
}
