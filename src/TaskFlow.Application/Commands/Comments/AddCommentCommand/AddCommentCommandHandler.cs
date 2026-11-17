using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Notifications;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Comments;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Comments;

/// <summary>Any logged-in user may comment on any task (every user can read every task).</summary>
public class AddCommentCommandHandler : ICommandHandler<AddCommentCommand, Result<CommentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;
    private readonly TaskNotifications _taskNotifications;

    public AddCommentCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService,
        TaskNotifications taskNotifications)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
        _taskNotifications = taskNotifications;
    }

    public async Task<Result<CommentDto>> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out _))
            return Result<CommentDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var taskId = command.TaskId;
        // Only what the notifications need, not the whole task.
        var task = (await _unitOfWork.TasksItems.ListAsync(t => t.Id == taskId,
            t => new { t.Title, t.CreatedByUserId, t.AssignedToUserId }, cancellationToken)).SingleOrDefault();
        if (task == null)
            return Result<CommentDto>.NotFound("Task not found");

        var comment = new Comment { TaskId = taskId, UserId = actorId };
        comment.Edit(command.Content);

        await _unitOfWork.Comments.AddAsync(comment, cancellationToken);
        await _taskNotifications.CommentAddedAsync(actorId, taskId, task.Title, task.CreatedByUserId, task.AssignedToUserId,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _taskNotifications.PublishAsync(cancellationToken);

        var commentDto = await _unitOfWork.Comments.GetDtoAsync(comment.Id, cancellationToken);
        return Result<CommentDto>.Success(commentDto!);
    }
}
