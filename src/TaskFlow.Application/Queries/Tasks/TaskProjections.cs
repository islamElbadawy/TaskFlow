using System.Linq.Expressions;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Tasks;

public static class TaskProjections
{
    /// <summary>One task with creator/assignee names and the comment count, translated to a single SQL query.</summary>
    public static readonly Expression<Func<TaskItem, TaskDto>> ToDto = t => new TaskDto(
        t.Id,
        t.Title,
        t.Description,
        t.Status.ToString(),
        t.Priority.ToString(),
        t.DueDate,
        t.CreatedByUserId,
        t.CreatedBy.FirstName + " " + t.CreatedBy.LastName,
        t.AssignedToUserId,
        t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : null,
        t.CreatedAt,
        t.UpdatedAt,
        t.Comments.Count);

    /// <summary>A task as shown in lists (no description), translated to SQL. <paramref name="now"/> decides
    /// <see cref="TaskListDto.IsOverdue"/>, so one request uses one clock for its filters and its rows.</summary>
    public static Expression<Func<TaskItem, TaskListDto>> ToListDto(DateTime now) => t => new TaskListDto(
        t.Id,
        t.Title,
        t.Status.ToString(),
        t.Priority.ToString(),
        t.DueDate,
        t.DueDate != null && t.DueDate < now && t.Status != TaskItemStatus.Done,
        t.CreatedByUserId,
        t.CreatedBy.FirstName + " " + t.CreatedBy.LastName,
        t.AssignedToUserId,
        t.AssignedTo != null ? t.AssignedTo.FirstName + " " + t.AssignedTo.LastName : null,
        t.Comments.Count,
        t.CreatedAt,
        t.UpdatedAt);

    /// <summary>Projected, no-tracking read of one task, or null if it doesn't exist.</summary>
    public static async Task<TaskDto?> GetDtoAsync(this IRepository<TaskItem> tasks, Guid taskId,
        CancellationToken cancellationToken) =>
        (await tasks.ListAsync(t => t.Id == taskId, ToDto, cancellationToken)).SingleOrDefault();
}
