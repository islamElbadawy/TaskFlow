using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Notifications;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;

/// <summary>PUT /api/tasks/{id}: replaces every editable field. Creator, Manager or Admin only.</summary>
public class UpdateTaskCommandHandler : ICommandHandler<UpdateTaskCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TaskNotifications _taskNotifications;

    public UpdateTaskCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService,
        TaskNotifications taskNotifications)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _taskNotifications = taskNotifications;
    }

    public async Task<Result<TaskDto>> HandleAsync(UpdateTaskCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<TaskDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        // The validator rejects unknown names; never keep the old value silently for an invalid one.
        if (!Enum.TryParse<TaskItemStatus>(command.Status, true, out var status) || !Enum.IsDefined(status))
            return Result<TaskDto>.Validation("Status must be one of: Todo, InProgress, Done");

        if (!Enum.TryParse<TaskPriority>(command.Priority, true, out var priority) || !Enum.IsDefined(priority))
            return Result<TaskDto>.Validation("Priority must be one of: Low, Medium, High");

        var task = await _unitOfWork.TasksItems.GetByIdAsync(command.TaskId, cancellationToken);
        if (task == null)
            return Result<TaskDto>.NotFound("Task not found");

        if (!task.CanBeEditedBy(actorId, actorRole))
            return Result<TaskDto>.Forbidden("Only the task's creator, a Manager or an Admin can edit this task.");

        // Only a new assignee is checked; keeping the current one (even if since deactivated) is allowed.
        if (command.AssignedToId is { } assigneeId && assigneeId != task.AssignedToUserId)
        {
            var assigneeError = await _unitOfWork.CheckAssigneeAsync(assigneeId, cancellationToken);
            if (assigneeError != null)
                return Result<TaskDto>.Validation(assigneeError);
        }

        var before = (task.Title, task.Description, task.Priority, task.DueDate, task.Status, task.AssignedToUserId);

        task.UpdateDetails(command.Title, command.Description, priority, command.DueDate);
        task.ChangeStatus(status);
        task.AssignTo(command.AssignedToId);

        // Notifications go into the same SaveChanges as the change (order = priority, one per user).
        if (task.AssignedToUserId != before.AssignedToUserId)
            await _taskNotifications.TaskAssignedAsync(actorId, task.Id, task.Title, task.AssignedToUserId, cancellationToken);
        if (task.Status == TaskItemStatus.Done && before.Status != TaskItemStatus.Done)
            await _taskNotifications.TaskCompletedAsync(actorId, task.Id, task.Title, task.CreatedByUserId, cancellationToken);
        if ((task.Title, task.Description, task.Priority, task.DueDate, task.Status, task.AssignedToUserId) != before)
            await _taskNotifications.TaskUpdatedAsync(actorId, task.Id, task.Title, task.AssignedToUserId, cancellationToken);

        // The task is tracked, so SaveChanges writes only the columns that changed.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _taskNotifications.PublishAsync(cancellationToken);

        var taskDto = await _unitOfWork.TasksItems.GetDtoAsync(task.Id, cancellationToken);
        return Result<TaskDto>.Success(taskDto!);
    }
}
