using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;

namespace TaskFlow.Application.Queries.Comments;

/// <summary>GET /api/tasks/{taskId}/comments?page=1&amp;pageSize=20, oldest first.</summary>
public class GetTaskCommentsQuery : IQuery<Result<PaginatedList<CommentDto>>>
{
    public const int MaxPageSize = 100;

    /// <summary>Set from the route by the controller.</summary>
    public Guid TaskId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
