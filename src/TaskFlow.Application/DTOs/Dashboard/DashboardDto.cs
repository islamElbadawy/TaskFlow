namespace TaskFlow.Application.DTOs.Dashboard;

public record DashboardDto(
    int TotalTasks,
    int TodoTasks,
    int InProgressTasks,
    int CompletedTasks,
    int OverdueTasks,
    List<TaskListDto> RecentTasks,
    List<TaskListDto> MyTasks
);