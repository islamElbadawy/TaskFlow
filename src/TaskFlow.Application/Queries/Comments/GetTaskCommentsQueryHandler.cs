using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Comments;

public class GetTaskCommentsQueryHandler : IQueryHandler<GetTaskCommentsQuery, Result<PaginatedList<CommentDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTaskCommentsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaginatedList<CommentDto>>> HandleAsync(GetTaskCommentsQuery query,
        CancellationToken cancellationToken = default)
    {
        var taskId = query.TaskId;
        if (!await _unitOfWork.TasksItems.AnyAsync(t => t.Id == taskId, cancellationToken))
            return Result<PaginatedList<CommentDto>>.NotFound("Task not found");

        var totalCount = await _unitOfWork.Comments.CountAsync(c => c.TaskId == taskId, cancellationToken);

        // Oldest first, so the thread reads top to bottom.
        var items = await _unitOfWork.Comments.ListAsync(
            c => c.TaskId == taskId,
            orderBy: c => c.CreatedAt,
            descending: false,
            skip: (query.Page - 1) * query.PageSize,
            take: query.PageSize,
            selector: CommentProjections.ToDto,
            cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        return Result<PaginatedList<CommentDto>>.Success(
            new PaginatedList<CommentDto>(items.ToList(), query.Page, query.PageSize, totalCount, totalPages));
    }
}
