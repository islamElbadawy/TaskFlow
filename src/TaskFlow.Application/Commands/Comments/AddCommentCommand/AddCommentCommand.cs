using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Comments;

/// <summary>POST /api/tasks/{taskId}/comments, body <c>{ "content": "..." }</c>. The author is the caller.</summary>
public class AddCommentCommand : ICommand<Result<CommentDto>>
{
    /// <summary>Set from the route by the controller.</summary>
    public Guid TaskId { get; set; }

    public string Content { get; set; } = string.Empty;
}
