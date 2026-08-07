using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Commands.Users;
using TaskFlow.Application.Commands.Users.auth;
using TaskFlow.Application.Commands.Users.LoginCommand;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.DTOs.Auth;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICurrentUserService _currentUserService;

    public AuthController(ICommandDispatcher commandDispatcher, ICurrentUserService currentUserService)
    {
        _commandDispatcher = commandDispatcher;
        _currentUserService = currentUserService;
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

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (request == null)
            return BadRequest("Request body cannot be empty.");

        var userId = _currentUserService.UserId;
        var command = new ChangePassowrdCommand
        {
            UserId = userId.Value,
            CurrentPassword = request.CurrentPassword,
            NewPassword = request.NewPassword
        };

        var result = await _commandDispatcher.DispatchAsync(command);
        if (!result.IsSuccess)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result);
    }
}