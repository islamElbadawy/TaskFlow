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
        var task = await _unitOfWork.TasksItems.FirstOrDefaultAsync(t => t.Id == command.TaskId, cancellationToken);
        
        if (task == null)
            return Result<TaskDto>.NotFound("Task not found");

        if (string.IsNullOrWhiteSpace(command.Title))
            return Result<TaskDto>.Validation("Title is required");

        var previousAssignedToId = task.AssignedToUserId;
        
        task.Title = command.Title;
        task.Description = command.Description;
        task.DueDate = command.DueDate;
        task.AssignedToUserId = command.AssignedToId;
        
        if (Enum.TryParse<TaskItemStatus>(command.Status, true, out var status))
            task.Status = status;

        if (Enum.TryParse<TaskPriority>(command.Priority, true, out var priority))
            task.Priority = priority;

        await _unitOfWork.TasksItems.UpdateAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        // The generic repository doesn't Include navigations, so read the related names and the comment
        // count with one projected query instead of dereferencing unloaded navigation properties.
        var details = (await _unitOfWork.TasksItems.ListAsync(
            t => t.Id == task.Id,
            t => new
            {
                CreatedByName = t.CreatedBy.FirstName + " " + t.CreatedBy.LastName,
                AssignedToName = t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : null,
                CommentsCount = t.Comments.Count
            },
            cancellationToken)).Single();

        var taskDto = new TaskDto(
            Id: task.Id,
            Title: task.Title,
            Description: task.Description,
            Status: task.Status.ToString(),
            Priority: task.Priority.ToString(),
            DueDate: task.DueDate,
            CreatedById: task.CreatedByUserId,
            CreatedByName: details.CreatedByName,
            AssignedToId: task.AssignedToUserId,
            AssignedToName: details.AssignedToName,
            CreatedAt: task.CreatedAt,
            UpdatedAt: task.UpdatedAt,
            CommentsCount: details.CommentsCount
        );

        return Result<TaskDto>.Success(taskDto);
    }
}