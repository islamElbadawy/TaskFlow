using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Common;
using TaskFlow.Application.Commands.Tasks;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Tasks;

namespace TaskFlow.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ICurrentUserService _currentUserService;

    public TasksController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        ICurrentUserService currentUserService)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _currentUserService = currentUserService;
    }


    /// <summary>Lists tasks, newest first. Optional filters: status, priority, assignedToId, unassigned,
    /// createdById, overdue; paging with page (from 1) and pageSize (1-100, default 20).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] GetTasksQuery query)
    {
        var result = await _queryDispatcher.DispatchAsync(query, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _queryDispatcher.DispatchAsync(new GetTaskByIdQuery { TaskId = id }, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskCommand command)
    {
        command.CreatedById = _currentUserService.UserId.Value;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }
}
