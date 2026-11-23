using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class OutboxController : ControllerBase
{
    private readonly IOutboxProcessor _outboxProcessor;

    public OutboxController(IOutboxProcessor outboxProcessor)
    {
        _outboxProcessor = outboxProcessor;
    }

    /// <summary>Processes due Email outbox rows immediately (the background worker also polls every Outbox:IntervalMinutes).</summary>
    [HttpPost("process")]
    public async Task<IActionResult> Process(CancellationToken cancellationToken)
    {
        var delivered = await _outboxProcessor.ProcessDueEmailsAsync(cancellationToken);
        return Ok(new { isSuccess = true, data = new { delivered } });
    }
}
