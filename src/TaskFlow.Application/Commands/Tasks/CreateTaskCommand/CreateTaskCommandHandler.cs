using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Notifications;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

public class CreateTaskCommandHandler : ICommandHandler<CreateTaskCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly TaskNotifications _taskNotifications;

    public CreateTaskCommandHandler(IUnitOfWork unitOfWork, TaskNotifications taskNotifications)
    {
        _unitOfWork = unitOfWork;
        _taskNotifications = taskNotifications;
    }

    public async Task<Result<TaskDto>> HandleAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        // Required fields and lengths are checked by CreateTaskCommandValidator (run by the dispatcher).
        var createdBy = await _unitOfWork.Users.GetByIdAsync(command.CreatedById, cancellationToken);
        if (createdBy == null)
            return Result<TaskDto>.NotFound("Creator user not found");

        User? assignedTo = null;
        if (command.AssignedToId.HasValue)
        {
            assignedTo = await _unitOfWork.Users.GetByIdAsync(command.AssignedToId.Value, cancellationToken);
            if (assignedTo == null)
                return Result<TaskDto>.Validation("Assigned user not found");

            if (!assignedTo.IsActive)
                return Result<TaskDto>.Validation("Assigned user is inactive");
        }

        // The validator rejects unknown names; never fall back to a default (e.g. "Urgent" used to become Medium).
        if (!Enum.TryParse<TaskPriority>(command.Priority, true, out var priority) || !Enum.IsDefined(priority))
            return Result<TaskDto>.Validation("Priority must be one of: Low, Medium, High");

        var taskItem = new TaskItem
        {
            Title = command.Title,
            Description = command.Description,
            Priority = priority,
            Status = TaskItemStatus.Todo,
            // New tasks go to the bottom of the Todo column.
            Position = await _unitOfWork.TasksItems.NextPositionAsync(TaskItemStatus.Todo, cancellationToken),
            DueDate = command.DueDate,
            CreatedByUserId = command.CreatedById,
            AssignedToUserId = command.AssignedToId
        };

        await _unitOfWork.TasksItems.AddAsync(taskItem, cancellationToken);
        // Saved together with the task, pushed after the save.
        await _taskNotifications.TaskAssignedAsync(command.CreatedById, taskItem.Id, taskItem.Title,
            taskItem.AssignedToUserId, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _taskNotifications.PublishAsync(cancellationToken);

        var taskDto = new TaskDto(
            Id: taskItem.Id,
            Title: taskItem.Title,
            Description: taskItem.Description,
            Status: taskItem.Status.ToString(),
            Position: taskItem.Position,
            Priority: taskItem.Priority.ToString(),
            DueDate: taskItem.DueDate,
            CreatedById: taskItem.CreatedByUserId,
            CreatedByName: $"{createdBy.FirstName} {createdBy.LastName}",
            AssignedToId: taskItem.AssignedToUserId,
            AssignedToName: assignedTo != null ? $"{assignedTo.FirstName} {assignedTo.LastName}" : null,
            CreatedAt: taskItem.CreatedAt,
            UpdatedAt: taskItem.UpdatedAt,
            CommentsCount: 0
        );

        return Result<TaskDto>.Success(taskDto);
    }
}