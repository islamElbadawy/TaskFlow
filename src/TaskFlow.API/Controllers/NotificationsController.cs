using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.API.Common;
using TaskFlow.Application.Commands.Notifications;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Queries.Notifications;

namespace TaskFlow.API.Controllers;

/// <summary>The caller's own notifications. They are created automatically by task and comment changes and
/// pushed live over SignalR (<c>/hubs/notifications</c>, method <c>ReceiveNotification</c>).</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IQueryDispatcher _queryDispatcher;

    public NotificationsController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
    {
        _commandDispatcher = commandDispatcher;
        _queryDispatcher = queryDispatcher;
    }

    /// <summary>Newest first. <c>unread=true</c> only unread, <c>unread=false</c> only read; paging page, pageSize (1-100).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] GetMyNotificationsQuery query)
    {
        var result = await _queryDispatcher.DispatchAsync(query, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var result = await _queryDispatcher.DispatchAsync(new GetUnreadNotificationCountQuery(), HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }

    /// <summary>Marks one notification as read (idempotent). 404 if it doesn't exist or isn't yours.</summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var result = await _commandDispatcher.DispatchAsync(
            new MarkNotificationReadCommand { NotificationId = id }, HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }

    /// <summary>Marks all of your unread notifications as read; returns how many changed.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var result = await _commandDispatcher.DispatchAsync(new MarkAllNotificationsReadCommand(), HttpContext.RequestAborted);
        return this.ToActionResult(result);
    }
}
