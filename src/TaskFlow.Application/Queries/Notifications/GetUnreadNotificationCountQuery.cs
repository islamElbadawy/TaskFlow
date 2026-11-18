using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Notifications;

/// <summary>GET /api/notifications/unread-count: how many of the caller's notifications are unread.</summary>
public class GetUnreadNotificationCountQuery : IQuery<Result<int>>
{
}

public class GetUnreadNotificationCountQueryHandler : IQueryHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetUnreadNotificationCountQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> HandleAsync(GetUnreadNotificationCountQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_currentUserService.UserId is not { } userId)
            return Result<int>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var count = await _unitOfWork.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
        return Result<int>.Success(count);
    }
}
