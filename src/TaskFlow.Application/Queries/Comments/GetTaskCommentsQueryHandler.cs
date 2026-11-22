using System.Linq.Expressions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Comments;

public class GetTaskCommentsQueryHandler : IQueryHandler<GetTaskCommentsQuery, Result<PaginatedList<CommentDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetTaskCommentsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginatedList<CommentDto>>> HandleAsync(GetTaskCommentsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<PaginatedList<CommentDto>>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var isAdmin = actorRole == UserRole.Admin;
        var taskId = query.TaskId;
        if (!await _unitOfWork.TasksItems.AnyAsync(t => t.Id == taskId, cancellationToken))
            return Result<PaginatedList<CommentDto>>.NotFound("Task not found");

        Expression<Func<Comment, bool>> filter = c =>
            c.TaskId == taskId && (isAdmin || c.Task.CreatedByUserId == actorId || c.Task.AssignedToUserId == actorId);

        var totalCount = await _unitOfWork.Comments.CountAsync(filter, cancellationToken);

        // Oldest first, so the thread reads top to bottom.
        var items = await _unitOfWork.Comments.ListAsync(
            filter,
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
