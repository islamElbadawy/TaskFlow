using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Notifications;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Notifications;

/// <summary>PATCH /api/notifications/{id}/read. Idempotent; another user's notification is reported as not found.</summary>
public class MarkNotificationReadCommand : ICommand<Result<NotificationDto>>
{
    public Guid NotificationId { get; set; }
}

public class MarkNotificationReadCommandHandler : ICommandHandler<MarkNotificationReadCommand, Result<NotificationDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationReadCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<NotificationDto>> HandleAsync(MarkNotificationReadCommand command,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not { } userId)
            return Result<NotificationDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        // Filtering by owner (not 403 for someone else's) doesn't reveal which notification ids exist.
        var notificationId = command.NotificationId;
        var notification = await _unitOfWork.Notifications.FirstOrDefaultAsync(
            n => n.Id == notificationId && n.UserId == userId, cancellationToken);
        if (notification == null)
            return Result<NotificationDto>.NotFound("Notification not found");

        if (!notification.IsRead)
        {
            notification.MarkAsRead();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<NotificationDto>.Success(NotificationProjections.ToDtoFromEntity(notification));
    }
}
