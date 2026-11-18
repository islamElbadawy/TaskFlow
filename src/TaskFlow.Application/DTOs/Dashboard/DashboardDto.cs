namespace TaskFlow.Application.DTOs.Dashboard;

/// <summary>
/// GET /api/dashboard: an overview for the caller. <see cref="Tasks"/>, <see cref="OverdueTasks"/> and
/// <see cref="DueSoonTasks"/> cover every task (every user can list all tasks); the rest is about the caller.
/// </summary>
/// <param name="Tasks">All tasks by status.</param>
/// <param name="OverdueTasks">Tasks past their due date that are not Done.</param>
/// <param name="DueSoonTasks">Tasks due within the next 7 days that are not Done.</param>
/// <param name="MyAssignedTasks">Tasks assigned to the caller, by status.</param>
/// <param name="MyOpenCreatedTasks">Tasks the caller created that are not Done.</param>
/// <param name="UnreadNotifications">The caller's unread notifications.</param>
/// <param name="RecentTasks">The 5 most recently updated (or created) tasks the caller created or is assigned to.</param>
public record DashboardDto(
    TaskStatusCountsDto Tasks,
    int OverdueTasks,
    int DueSoonTasks,
    TaskStatusCountsDto MyAssignedTasks,
    int MyOpenCreatedTasks,
    int UnreadNotifications,
    IReadOnlyList<TaskListDto> RecentTasks
);

/// <summary>Task counts per status; <see cref="Total"/> is their sum.</summary>
public record TaskStatusCountsDto(int Total, int Todo, int InProgress, int Done);
