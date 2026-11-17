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
        var task = (await _unitOfWork.TasksItems.ListAsync(
            t => t.Id == query.TaskId,
            t => new TaskDto(
                t.Id,
                t.Title,
                t.Description,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.DueDate,
                t.CreatedByUserId,
                t.CreatedBy.FirstName + " " + t.CreatedBy.LastName,
                t.AssignedToUserId,
                t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : null,
                t.CreatedAt,
                t.UpdatedAt,
                t.Comments.Count),
            cancellationToken)).SingleOrDefault();

        return task is null
            ? Result<TaskDto>.NotFound("Task not found")
            : Result<TaskDto>.Success(task);
    }
}
