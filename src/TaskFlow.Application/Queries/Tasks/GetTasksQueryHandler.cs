using System.Linq.Expressions;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Tasks;

public class GetTasksQueryHandler : IQueryHandler<GetTasksQuery, Result<PaginatedList<TaskListDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetTasksQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaginatedList<TaskListDto>>> HandleAsync(GetTasksQuery query,
        CancellationToken cancellationToken = default)
    {
        // Shape is validated by GetTasksQueryValidator; parse here only to get the typed values.
        TaskItemStatus? status = Enum.TryParse<TaskItemStatus>(query.Status, true, out var s) ? s : null;
        TaskPriority? priority = Enum.TryParse<TaskPriority>(query.Priority, true, out var p) ? p : null;
        var assignedToId = query.AssignedToId;
        var createdById = query.CreatedById;
        var unassigned = query.Unassigned;
        var overdue = query.Overdue;
        var now = DateTime.UtcNow;

        // Unused filters collapse to "true" in the generated SQL parameters, so one expression covers every combination.
        Expression<Func<TaskItem, bool>> filter = t =>
            (status == null || t.Status == status) &&
            (priority == null || t.Priority == priority) &&
            (assignedToId == null || t.AssignedToUserId == assignedToId) &&
            (!unassigned || t.AssignedToUserId == null) &&
            (createdById == null || t.CreatedByUserId == createdById) &&
            (!overdue || (t.DueDate != null && t.DueDate < now && t.Status != TaskItemStatus.Done));

        var totalCount = await _unitOfWork.TasksItems.CountAsync(filter, cancellationToken);

        var skip = (query.Page - 1) * query.PageSize;
        var items = string.Equals(query.Sort, GetTasksQuery.SortByPosition, StringComparison.OrdinalIgnoreCase)
            // Board order: columns left to right, then top to bottom; ties (gaps don't matter) by age, then id.
            ? await _unitOfWork.TasksItems.ListAsync(
                filter,
                orderBy: q => q
                    .OrderBy(t => t.Status == TaskItemStatus.Todo ? 0 : t.Status == TaskItemStatus.InProgress ? 1 : 2)
                    .ThenBy(t => t.Position)
                    .ThenBy(t => t.CreatedAt)
                    .ThenBy(t => t.Id),
                skip,
                query.PageSize,
                TaskProjections.ToListDto(now),
                cancellationToken)
            : await _unitOfWork.TasksItems.ListAsync(
                filter,
                orderBy: t => t.CreatedAt,
                descending: true,
                skip,
                take: query.PageSize,
                selector: TaskProjections.ToListDto(now),
                cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        return Result<PaginatedList<TaskListDto>>.Success(
            new PaginatedList<TaskListDto>(items.ToList(), query.Page, query.PageSize, totalCount, totalPages));
    }
}
