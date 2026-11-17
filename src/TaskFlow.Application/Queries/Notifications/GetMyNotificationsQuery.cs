using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;

namespace TaskFlow.Application.Queries.Notifications;

/// <summary>GET /api/notifications?unread=true&amp;page=1&amp;pageSize=20: the caller's own notifications, newest first.</summary>
public class GetMyNotificationsQuery : IQuery<Result<PaginatedList<NotificationDto>>>
{
    public const int MaxPageSize = 100;

    /// <summary>true: only unread; false: only read; omitted: all.</summary>
    public bool? Unread { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
