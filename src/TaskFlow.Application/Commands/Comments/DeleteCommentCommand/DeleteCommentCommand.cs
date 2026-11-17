using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.Commands.Comments;

/// <summary>DELETE /api/comments/{id}. Author or Admin.</summary>
public class DeleteCommentCommand : ICommand<Result<bool>>
{
    public Guid CommentId { get; set; }
}
