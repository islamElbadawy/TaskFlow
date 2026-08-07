using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;

public class UpdateTaskCommandHandler : ICommandHandler<UpdateTaskCommand, Result<TaskDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateTaskCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }
    public async Task<Result<TaskDto>> HandleAsync(UpdateTaskCommand command, CancellationToken cancellationToken)
    {
        var task = await _unitOfWork.TasksItems.FirstOrDefaultAsync(t => t.Id == command.TaskId);
        
        if (task == null)
            return Result<TaskDto>.Failure("Task not found");

        if (string.IsNullOrWhiteSpace(command.Title))
            return Result<TaskDto>.Failure("Title is required");

        var previousAssignedToId = task.AssignedToUserId;
        
        task.Title = command.Title;
        task.Description = command.Description;
        task.DueDate = command.DueDate;
        task.AssignedToUserId = command.AssignedToId;
        
        if (Enum.TryParse<TaskItemStatus>(command.Status, true, out var status))
            task.Status = status;

        if (Enum.TryParse<TaskPriority>(command.Priority, true, out var priority))
            task.Priority = priority;

        await _unitOfWork.TasksItems.UpdateAsync(task);
        await _unitOfWork.SaveChangesAsync();
        
        var commentsCount = await _unitOfWork.Comments.CountAsync(c => c.TaskId == task.Id);
        
        var taskDto = new TaskDto(
            Id: task.Id,
            Title: task.Title,
            Description: task.Description,
            Status: task.Status.ToString(),
            Priority: task.Priority.ToString(),
            DueDate: task.DueDate,
            CreatedById: task.CreatedByUserId,
            CreatedByName: $"{task.CreatedBy.FirstName} {task.CreatedBy.LastName}",
            AssignedToId: task.AssignedToUserId,
            AssignedToName: task.AssignedTo != null ? $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName}" : null,
            CreatedAt: task.CreatedAt,
            UpdatedAt: task.UpdatedAt,
            CommentsCount: commentsCount
        );

        return Result<TaskDto>.Success(taskDto);
    }
}