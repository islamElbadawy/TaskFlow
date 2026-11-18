using System.Linq.Expressions;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Queries.Notifications;

public static class NotificationProjections
{
    public static readonly Expression<Func<Notification, NotificationDto>> ToDto = n => new NotificationDto(
        n.Id,
        n.Message,
        n.Type.ToString(),
        n.IsRead,
        n.RelatedTaskId,
        n.CreatedAt);

    /// <summary>In-memory mapping, for entities that are already loaded (e.g. right after a save).</summary>
    public static NotificationDto ToDtoFromEntity(Notification n) =>
        new(n.Id, n.Message, n.Type.ToString(), n.IsRead, n.RelatedTaskId, n.CreatedAt);
}
