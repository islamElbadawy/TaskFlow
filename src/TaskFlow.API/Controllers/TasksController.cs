using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Commands.Tasks;
using TaskFlow.Application.Common.Interfaces;

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


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskCommand command)
    {
        command.CreatedById = _currentUserService.UserId.Value;
        var result = await _commandDispatcher.DispatchAsync(command);
        
        if (!result.IsSuccess)
            return BadRequest(result);
        
        return Ok(result);
    }
}