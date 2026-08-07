using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Tasks;

public class CreateTaskCommand: ICommand<Result<TaskDto>>
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public DateTime? DueDate { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? AssignedToId { get; set; }
}