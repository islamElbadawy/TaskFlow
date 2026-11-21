using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Notifications;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>
/// Moves a task to a place in a status column (drag and drop on the board). Same permission as a status change:
/// creator, assignee, Manager or Admin, also for a reorder inside the column. Both affected columns are renumbered
/// 0..n-1 and saved in one SaveChanges (one database transaction), together with the notifications of a status
/// change. Concurrent moves: last write wins.
/// </summary>
public class MoveTaskCommandHandler : ICommandHandler<MoveTaskCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TaskNotifications _taskNotifications;

    public MoveTaskCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService,
        TaskNotifications taskNotifications)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _taskNotifications = taskNotifications;
    }

    public async Task<Result<TaskDto>> HandleAsync(MoveTaskCommand command, CancellationToken cancellationToken)
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
                "Only the task's creator, its assignee, a Manager or an Admin can move this task.");

        var previousStatus = task.Status;

        // Both columns, tracked, without the task itself: only rows whose position changes are written.
        var others = (await _unitOfWork.TasksItems.FindAsync(
            t => t.Id != task.Id && (t.Status == status || t.Status == previousStatus), cancellationToken)).ToList();

        var target = others.Where(t => t.Status == status).InBoardOrder();
        var index = target.Count;
        if (command.BeforeTaskId is { } beforeId)
        {
            index = target.FindIndex(t => t.Id == beforeId);
            if (index < 0)
                return Result<TaskDto>.Validation($"beforeTaskId must be a task in the {status} column");
        }

        target.Insert(index, task);
        task.MoveTo(status, index);
        target.Renumber();

        if (status != previousStatus)
        {
            // Close the hole the task left in its old column.
            others.Where(t => t.Status == previousStatus).InBoardOrder().Renumber();
            await _taskNotifications.StatusChangedAsync(actorId, task, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _taskNotifications.PublishAsync(cancellationToken);

        var taskDto = await _unitOfWork.TasksItems.GetDtoAsync(task.Id, cancellationToken);
        return Result<TaskDto>.Success(taskDto!);
    }
}
