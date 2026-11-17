using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

public class CreateTaskCommandHandler : ICommandHandler<CreateTaskCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateTaskCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TaskDto>> HandleAsync(CreateTaskCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
            return Result<TaskDto>.Failure("Title is required");

        var createdBy = await _unitOfWork.Users.GetByIdAsync(command.CreatedById);
        if (createdBy == null)
            return Result<TaskDto>.Failure("Creator user not found");

        User? assignedTo = null;
        if (command.AssignedToId.HasValue)
        {
            assignedTo = await _unitOfWork.Users.GetByIdAsync(command.AssignedToId.Value);
            if (assignedTo == null)
                return Result<TaskDto>.Failure("Assigned user not found");
        }

        if (!Enum.TryParse<TaskPriority>(command.Priority, true, out var priority))
            priority = TaskPriority.Medium;

        var taskItem = new TaskItem
        {
            Title = command.Title,
            Description = command.Description,
            Priority = priority,
            Status = TaskItemStatus.Todo,
            DueDate = command.DueDate,
            CreatedByUserId = command.CreatedById,
            AssignedToUserId = command.AssignedToId
        };

        await _unitOfWork.TasksItems.AddAsync(taskItem);
        await _unitOfWork.SaveChangesAsync();

        var taskDto = new TaskDto(
            Id: taskItem.Id,
            Title: taskItem.Title,
            Description: taskItem.Description,
            Status: taskItem.Status.ToString(),
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