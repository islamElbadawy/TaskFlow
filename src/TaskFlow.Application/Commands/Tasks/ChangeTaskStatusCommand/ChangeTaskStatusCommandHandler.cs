using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Notifications;
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
    private readonly TaskNotifications _taskNotifications;

    public ChangeTaskStatusCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService,
        TaskNotifications taskNotifications)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _taskNotifications = taskNotifications;
    }

    public async Task<Result<TaskDto>> HandleAsync(ChangeTaskStatusCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<TaskDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        if (!Enum.TryParse<TaskItemStatus>(command.Status, true, out var status) || !Enum.IsDefined(status))
            return Result<TaskDto>.Validation("Status must be one of: Todo, InProgress, Done");

        var task = await _unitOfWork.TasksItems.GetByIdAsync(command.TaskId, cancellationToken);
        if (task == null)
            return Result<TaskDto>.NotFound("Task not found");

        if (!task.CanChangeStatusBy(actorId, actorRole))
            return Result<TaskDto>.Forbidden(
                "Only the task's creator, its assignee, a Manager or an Admin can change its status.");

        // A real change moves the task to the bottom of its new column; the same status changes nothing.
        if (status != task.Status)
        {
            task.MoveTo(status, await _unitOfWork.TasksItems.NextPositionAsync(status, cancellationToken));
            await _taskNotifications.StatusChangedAsync(actorId, task, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _taskNotifications.PublishAsync(cancellationToken);

        var taskDto = await _unitOfWork.TasksItems.GetDtoAsync(task.Id, cancellationToken);
        return Result<TaskDto>.Success(taskDto!);
    }
}
