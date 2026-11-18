using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Dashboard;

namespace TaskFlow.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IQueryDispatcher _queryDispatcher;

    public DashboardController(IQueryDispatcher queryDispatcher)
    {
        _queryDispatcher = queryDispatcher;
    }

    /// <summary>All tasks by status, overdue and due within 7 days; the caller's assigned tasks by status, open
    /// tasks they created, unread notifications and the 5 most recently updated tasks they are involved in.</summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await _queryDispatcher.DispatchAsync(new GetDashboardQuery(), HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }
}
