using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>Creator, assignee, Manager or Admin may change the status.</summary>
public class ChangeTaskStatusCommandHandler : ICommandHandler<ChangeTaskStatusCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ChangeTaskStatusCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TaskDto>> HandleAsync(ChangeTaskStatusCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<TaskDto>.Unauthorized(TaskCommandGuards.NoActorMessage);

        if (!Enum.TryParse<TaskItemStatus>(command.Status, true, out var status) || !Enum.IsDefined(status))
            return Result<TaskDto>.Validation("Status must be one of: Todo, InProgress, Done");

        var task = await _unitOfWork.TasksItems.GetByIdAsync(command.TaskId, cancellationToken);
        if (task == null)
            return Result<TaskDto>.NotFound("Task not found");

        if (!task.CanChangeStatusBy(actorId, actorRole))
            return Result<TaskDto>.Forbidden(
                "Only the task's creator, its assignee, a Manager or an Admin can change its status.");

        task.ChangeStatus(status);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var taskDto = await _unitOfWork.TasksItems.GetDtoAsync(task.Id, cancellationToken);
        return Result<TaskDto>.Success(taskDto!);
    }
}
