using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Comments;

/// <summary>PUT /api/comments/{id}, body <c>{ "content": "..." }</c>. Author only.</summary>
public class UpdateCommentCommand : ICommand<Result<CommentDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    public Guid CommentId { get; set; }

    public string Content { get; set; } = string.Empty;
}
