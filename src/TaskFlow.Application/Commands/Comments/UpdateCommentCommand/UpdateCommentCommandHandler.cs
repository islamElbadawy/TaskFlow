using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Comments;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Comments;

/// <summary>Only the author may edit a comment (not even an Admin, so nobody can put words in someone's mouth).</summary>
public class UpdateCommentCommandHandler : ICommandHandler<UpdateCommentCommand, Result<CommentDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public UpdateCommentCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CommentDto>> HandleAsync(UpdateCommentCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out _))
            return Result<CommentDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var comment = await _unitOfWork.Comments.GetByIdAsync(command.CommentId, cancellationToken);
        if (comment == null)
            return Result<CommentDto>.NotFound("Comment not found");

        if (!comment.CanBeEditedBy(actorId))
            return Result<CommentDto>.Forbidden("Only the author can edit this comment.");

        comment.Edit(command.Content);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var commentDto = await _unitOfWork.Comments.GetDtoAsync(comment.Id, cancellationToken);
        return Result<CommentDto>.Success(commentDto!);
    }
}
