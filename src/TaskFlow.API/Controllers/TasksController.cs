using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Common;
using TaskFlow.Application.Commands.Tasks;
using TaskFlow.Application.Commands.Tasks.UpdateTaskCommand;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
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
    private readonly IRealTimeNotifier _realTimeNotifier;

    public TasksController(
        ICommandDispatcher commandDispatcher,
        IQueryDispatcher queryDispatcher,
        ICurrentUserService currentUserService,
        IRealTimeNotifier realTimeNotifier)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _currentUserService = currentUserService;
        _realTimeNotifier = realTimeNotifier;
    }


    /// <summary>Lists tasks, newest first (sort=createdAt) or in board order (sort=position: by status, then
    /// position). Optional filters: status, priority, assignedToId, unassigned, createdById, overdue; paging with
    /// page (from 1) and pageSize (1-100, default 20).</summary>
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
        if (_currentUserService.UserId is not { } userId)
            return this.Problem(ErrorType.Unauthorized, "The access token does not identify a user.");

        command.CreatedById = userId;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        if (result.IsSuccess && result.Data is not null)
            await _realTimeNotifier.NotifyBoardChangedAsync(new BoardChangeDto(result.Data.Id, "created"));
        return this.ToActionResult(result);
    }

    /// <summary>Replaces title, description, priority, due date, assignee and status. Creator, Manager or Admin;
    /// 404 unknown task, 403 not allowed, 400 unknown or inactive assignee.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskCommand command)
    {
        command.TaskId = id;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        if (result.IsSuccess)
            await _realTimeNotifier.NotifyBoardChangedAsync(new BoardChangeDto(id, "updated"));
        return this.ToActionResult(result);
    }

    /// <summary>Body <c>{ "status": "InProgress" }</c>. Creator, assignee, Manager or Admin.</summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeTaskStatusCommand command)
    {
        command.TaskId = id;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        if (result.IsSuccess)
            await _realTimeNotifier.NotifyBoardChangedAsync(new BoardChangeDto(id, "moved"));
        return this.ToActionResult(result);
    }

    /// <summary>Body <c>{ "assignedToId": "&lt;guid&gt;" }</c>, or <c>null</c> to unassign. Creator, Manager or Admin;
    /// 400 when the user is unknown or inactive.</summary>
    [HttpPatch("{id:guid}/assignee")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTaskCommand command)
    {
        command.TaskId = id;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        if (result.IsSuccess)
            await _realTimeNotifier.NotifyBoardChangedAsync(new BoardChangeDto(id, "updated"));
        return this.ToActionResult(result);
    }

    /// <summary>Body <c>{ "status": "Done", "beforeTaskId": "&lt;guid&gt;" }</c>: puts the task directly above
    /// beforeTaskId in that column, or at the end when it is null. Creator, assignee, Manager or Admin; 400 when
    /// beforeTaskId is the task itself or not in the target column.</summary>
    [HttpPatch("{id:guid}/move")]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveTaskCommand command)
    {
        command.TaskId = id;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        if (result.IsSuccess)
            await _realTimeNotifier.NotifyBoardChangedAsync(new BoardChangeDto(id, "moved"));
        return this.ToActionResult(result);
    }

    /// <summary>Creator or Admin. Comments are deleted with the task; notifications stay, without the task link.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _commandDispatcher.DispatchAsync(new DeleteTaskCommand { TaskId = id }, HttpContext.RequestAborted);
        if (result.IsSuccess)
            await _realTimeNotifier.NotifyBoardChangedAsync(new BoardChangeDto(id, "deleted"));
        return this.ToActionResult(result);
    }
}
