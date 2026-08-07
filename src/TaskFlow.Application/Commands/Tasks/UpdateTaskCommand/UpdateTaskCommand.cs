using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;

public class UpdateTaskCommand : ICommand<Result<TaskDto>>
{
    public int TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int? AssignedToId { get; set; }
}