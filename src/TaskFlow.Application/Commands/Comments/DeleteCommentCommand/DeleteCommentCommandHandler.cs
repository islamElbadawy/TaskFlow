using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Comments;

/// <summary>The author or an Admin (moderation) may delete a comment.</summary>
public class DeleteCommentCommandHandler : ICommandHandler<DeleteCommentCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteCommentCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> HandleAsync(DeleteCommentCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<bool>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var comment = await _unitOfWork.Comments.GetByIdAsync(command.CommentId, cancellationToken);
        if (comment == null)
            return Result<bool>.NotFound("Comment not found");

        if (!comment.CanBeDeletedBy(actorId, actorRole))
            return Result<bool>.Forbidden("Only the author or an Admin can delete this comment.");

        await _unitOfWork.Comments.DeleteAsync(comment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
