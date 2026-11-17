using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Tasks;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>Creator, Manager or Admin may (re)assign or unassign a task.</summary>
public class AssignTaskCommandHandler : ICommandHandler<AssignTaskCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public AssignTaskCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TaskDto>> HandleAsync(AssignTaskCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<TaskDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var task = await _unitOfWork.TasksItems.GetByIdAsync(command.TaskId, cancellationToken);
        if (task == null)
            return Result<TaskDto>.NotFound("Task not found");

        if (!task.CanBeEditedBy(actorId, actorRole))
            return Result<TaskDto>.Forbidden("Only the task's creator, a Manager or an Admin can assign this task.");

        if (command.AssignedToId is { } assigneeId && assigneeId != task.AssignedToUserId)
        {
            var assigneeError = await _unitOfWork.CheckAssigneeAsync(assigneeId, cancellationToken);
            if (assigneeError != null)
                return Result<TaskDto>.Validation(assigneeError);
        }

        task.AssignTo(command.AssignedToId);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var taskDto = await _unitOfWork.TasksItems.GetDtoAsync(task.Id, cancellationToken);
        return Result<TaskDto>.Success(taskDto!);
    }
}
