using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Commands.Users;
using TaskFlow.Application.Commands.Users.LoginCommand;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;

    public AuthController(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
    {
        var result = await _commandDispatcher.DispatchAsync(command);
        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Register([FromBody] LoginCommand command)
    {
        var result = await _commandDispatcher.DispatchAsync(command);
        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }
}