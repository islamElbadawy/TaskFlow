using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Notifications;

/// <summary>POST /api/notifications/read-all. Returns how many notifications were marked as read.</summary>
public class MarkAllNotificationsReadCommand : ICommand<Result<int>>
{
}

public class MarkAllNotificationsReadCommandHandler : ICommandHandler<MarkAllNotificationsReadCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public MarkAllNotificationsReadCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> HandleAsync(MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId is not { } userId)
            return Result<int>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var unread = (await _unitOfWork.Notifications.FindAsync(n => n.UserId == userId && !n.IsRead, cancellationToken))
            .ToList();
        if (unread.Count == 0)
            return Result<int>.Success(0);

        foreach (var notification in unread)
            notification.MarkAsRead();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(unread.Count);
    }
}
