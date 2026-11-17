using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Commands.Users.DeleteUserCommand;
using TaskFlow.Application.Commands.Users.UpdateUserCommand;
using TaskFlow.Application.Commands.Users.UpdateUserRoleCommand;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Users;

namespace TaskFlow.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher,
        ICurrentUserService currentUserService)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
        _currentUserService = currentUserService;
    }

    [HttpGet()]
    public async Task<IActionResult> GetAll()
    {
        var result = await _queryDispatcher.DispatchAsync(new GetAllUsersQuery());
        if (!result.IsSuccess)
            return BadRequest(result);
        return Ok(result);
    }


    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
            return Unauthorized();

        var query = new GetUserByIdQuery { UserId = userId.Value };
        var result = await _queryDispatcher.DispatchAsync(query);

        if (!result.IsSuccess)
            return NotFound(result);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var query = new GetUserByIdQuery { UserId = id };
        var result = await _queryDispatcher.DispatchAsync(query);
        if (!result.IsSuccess)
            return NotFound(result);
        return Ok(result);
    }

    // PUT is the correct verb for an update; POST is kept temporarily so existing clients don't break.
    [HttpPut("{id}")]
    [HttpPost("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserCommand command)
    {
        if (!CanManageUser(id))
            return Forbid();

        command.UserId = id;
        var result = await _commandDispatcher.DispatchAsync(command);

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleCommand command)
    {
        if (command == null)
            return BadRequest(Result<bool>.Failure("Request body cannot be empty"));

        command.UserId = id;
        var result = await _commandDispatcher.DispatchAsync(command);

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        if (!CanManageUser(id))
            return Forbid();

        var command = new DeleteUserCommand { UserId = id };

        var result = await _commandDispatcher.DispatchAsync(command);

        if (!result.IsSuccess)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>A user may manage only their own account; admins may manage any account.</summary>
    private bool CanManageUser(Guid targetUserId) =>
        _currentUserService.UserId == targetUserId || User.IsInRole("Admin");
}