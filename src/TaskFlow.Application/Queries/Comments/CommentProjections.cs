using System.Linq.Expressions;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Comments;

public static class CommentProjections
{
    /// <summary>A comment with its author's name, translated to SQL (no entity loading).</summary>
    public static readonly Expression<Func<Comment, CommentDto>> ToDto = c => new CommentDto(
        c.Id,
        c.Content,
        c.TaskId,
        c.UserId,
        c.User.FirstName + " " + c.User.LastName,
        c.CreatedAt,
        c.UpdatedAt);

    /// <summary>Projected, no-tracking read of one comment, or null if it doesn't exist.</summary>
    public static async Task<CommentDto?> GetDtoAsync(this IRepository<Comment> comments, Guid commentId,
        CancellationToken cancellationToken) =>
        (await comments.ListAsync(c => c.Id == commentId, ToDto, cancellationToken)).SingleOrDefault();
}
