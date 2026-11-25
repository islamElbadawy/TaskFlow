using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Tasks;

public class GetTaskByIdQueryHandler : IQueryHandler<GetTaskByIdQuery, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetTaskByIdQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TaskDto>> HandleAsync(GetTaskByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<TaskDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        // One projected query: the task, its creator/assignee names and the comment count.
        var task = await _unitOfWork.TasksItems.GetDtoAsync(query.TaskId, cancellationToken);
        if (task is null)
            return Result<TaskDto>.NotFound("Task not found");

        if (actorRole != UserRole.Admin && task.CreatedById != actorId && task.AssignedToId != actorId)
            return Result<TaskDto>.Forbidden("You do not have access to this task.");

        return Result<TaskDto>.Success(task);
    }
}
