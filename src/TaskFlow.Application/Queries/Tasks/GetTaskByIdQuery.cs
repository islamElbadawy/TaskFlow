using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Queries.Tasks;

public class GetTaskByIdQuery : IQuery<Result<TaskDto>>
{
    public Guid TaskId { get; set; }
}
