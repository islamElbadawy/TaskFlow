using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Common;
using TaskFlow.Application.Commands.Reminders.RunDeadlineRemindersCommand;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class RemindersController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;

    public RemindersController(ICommandDispatcher commandDispatcher)
    {
        _commandDispatcher = commandDispatcher;
    }

    /// <summary>Runs the deadline reminders now (the background job also runs them every Reminders:IntervalMinutes).
    /// Returns how many reminders were created and for which tasks.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run()
    {
        var result = await _commandDispatcher.DispatchAsync(new RunDeadlineRemindersCommand(), HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }
}
