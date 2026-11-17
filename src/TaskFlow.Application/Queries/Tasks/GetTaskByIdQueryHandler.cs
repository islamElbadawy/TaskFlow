using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Tasks;

public class GetTaskByIdQueryHandler : IQueryHandler<GetTaskByIdQuery, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTaskByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TaskDto>> HandleAsync(GetTaskByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        // One projected query: the task, its creator/assignee names and the comment count.
        var task = await _unitOfWork.TasksItems.GetDtoAsync(query.TaskId, cancellationToken);

        return task is null
            ? Result<TaskDto>.NotFound("Task not found")
            : Result<TaskDto>.Success(task);
    }
}
