using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Common;
using TaskFlow.Application.Commands.Comments;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Comments;

namespace TaskFlow.API.Controllers;

/// <summary>Comments are listed and created under their task (<c>/api/tasks/{taskId}/comments</c>).</summary>
[Authorize]
[ApiController]
[Route("api")]
public class CommentsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;

    public CommentsController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
    }

    /// <summary>A task's comments, oldest first, with the author's name. Paging: page (from 1), pageSize (1-100,
    /// default 20). 404 unknown task.</summary>
    [HttpGet("tasks/{taskId:guid}/comments")]
    public async Task<IActionResult> GetForTask(Guid taskId, [FromQuery] GetTaskCommentsQuery query)
    {
        query.TaskId = taskId;
        var result = await _queryDispatcher.DispatchAsync(query, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }

    /// <summary>Body <c>{ "content": "..." }</c> (1-1000 characters). Any logged-in user; the author is the caller.</summary>
    [HttpPost("tasks/{taskId:guid}/comments")]
    public async Task<IActionResult> Add(Guid taskId, [FromBody] AddCommentCommand command)
    {
        command.TaskId = taskId;
        var result = await _commandDispatcher.DispatchAsync(command, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }
}
