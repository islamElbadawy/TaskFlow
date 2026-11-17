using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
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

    public AddCommentCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CommentDto>> HandleAsync(AddCommentCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out _))
            return Result<CommentDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var taskId = command.TaskId;
        if (!await _unitOfWork.TasksItems.AnyAsync(t => t.Id == taskId, cancellationToken))
            return Result<CommentDto>.NotFound("Task not found");

        var comment = new Comment { TaskId = taskId, UserId = actorId };
        comment.Edit(command.Content);

        await _unitOfWork.Comments.AddAsync(comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var commentDto = await _unitOfWork.Comments.GetDtoAsync(comment.Id, cancellationToken);
        return Result<CommentDto>.Success(commentDto!);
    }
}
