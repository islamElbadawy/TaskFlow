using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs.Dashboard;
using TaskFlow.Application.Queries.Tasks;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Dashboard;

/// <summary>
/// Seven small read-only queries, run one after another on the request's DbContext: two GROUP BY status counts,
/// four COUNTs and one page of 5 projected tasks. The filters use indexed columns
/// (tasks.(assigned_to_user_id, status), tasks.created_by_user_id, notifications.(user_id, is_read)).
/// </summary>
public class GetDashboardQueryHandler : IQueryHandler<GetDashboardQuery, Result<DashboardDto>>
{
    public const int RecentTasksCount = 5;
    public static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(7);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetDashboardQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DashboardDto>> HandleAsync(GetDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_currentUserService.UserId is not { } userId)
            return Result<DashboardDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var tasks = _unitOfWork.TasksItems;
        var now = DateTime.UtcNow;
        var dueSoonEnd = now.Add(DueSoonWindow);

        var allByStatus = await tasks.CountByAsync(null, t => t.Status, cancellationToken);

        var overdue = await tasks.CountAsync(
            t => t.DueDate != null && t.DueDate < now && t.Status != TaskItemStatus.Done, cancellationToken);

        var dueSoon = await tasks.CountAsync(
            t => t.DueDate != null && t.DueDate >= now && t.DueDate <= dueSoonEnd && t.Status != TaskItemStatus.Done,
            cancellationToken);

        var assignedByStatus = await tasks.CountByAsync(t => t.AssignedToUserId == userId, t => t.Status,
            cancellationToken);

        var openCreated = await tasks.CountAsync(
            t => t.CreatedByUserId == userId && t.Status != TaskItemStatus.Done, cancellationToken);

        var unread = await _unitOfWork.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead,
            cancellationToken);

        var recent = await tasks.ListAsync(
            t => t.CreatedByUserId == userId || t.AssignedToUserId == userId,
            orderBy: t => t.UpdatedAt ?? t.CreatedAt,
            descending: true,
            skip: 0,
            take: RecentTasksCount,
            selector: TaskProjections.ToListDto(now),
            cancellationToken);

        return Result<DashboardDto>.Success(new DashboardDto(
            Tasks: ToStatusCounts(allByStatus),
            OverdueTasks: overdue,
            DueSoonTasks: dueSoon,
            MyAssignedTasks: ToStatusCounts(assignedByStatus),
            MyOpenCreatedTasks: openCreated,
            UnreadNotifications: unread,
            RecentTasks: recent));
    }

    private static TaskStatusCountsDto ToStatusCounts(IReadOnlyDictionary<TaskItemStatus, int> byStatus)
    {
        var todo = byStatus.GetValueOrDefault(TaskItemStatus.Todo);
        var inProgress = byStatus.GetValueOrDefault(TaskItemStatus.InProgress);
        var done = byStatus.GetValueOrDefault(TaskItemStatus.Done);
        return new TaskStatusCountsDto(todo + inProgress + done, todo, inProgress, done);
    }
}
