using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;

namespace TaskFlow.Application.Queries.Tasks;

/// <summary>
/// GET /api/tasks. Every filter is optional and they combine with AND. Bound from the query string,
/// e.g. <c>?status=InProgress&amp;assignedToId=...&amp;page=2&amp;pageSize=10</c>.
/// </summary>
public class GetTasksQuery : IQuery<Result<PaginatedList<TaskListDto>>>
{
    public const int MaxPageSize = 100;

    public const string SortByCreatedAt = "createdAt";
    public const string SortByPosition = "position";

    /// <summary>Todo, InProgress or Done (case-insensitive).</summary>
    public string? Status { get; set; }

    /// <summary>Low, Medium or High (case-insensitive).</summary>
    public string? Priority { get; set; }

    public Guid? AssignedToId { get; set; }

    /// <summary>true: only tasks assigned to nobody.</summary>
    public bool Unassigned { get; set; }

    public Guid? CreatedById { get; set; }

    /// <summary>true: only tasks past their due date that are not Done.</summary>
    public bool Overdue { get; set; }

    /// <summary>Only non-Done tasks due on or before this UTC timestamp.</summary>
    public DateTime? DueBefore { get; set; }

    /// <summary>Case-insensitive substring search across task title and description.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// createdAt (default): newest first. position: board order, i.e. by status (Todo, InProgress, Done), then by
    /// position in the column. Case-insensitive.
    /// </summary>
    public string? Sort { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
