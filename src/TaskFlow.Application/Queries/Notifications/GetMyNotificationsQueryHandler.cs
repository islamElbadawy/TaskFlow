using System.Linq.Expressions;
using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.DTOs.Pagination;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Notifications;

public class GetMyNotificationsQueryHandler : IQueryHandler<GetMyNotificationsQuery, Result<PaginatedList<NotificationDto>>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetMyNotificationsQueryHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginatedList<NotificationDto>>> HandleAsync(GetMyNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (_currentUserService.UserId is not { } userId)
            return Result<PaginatedList<NotificationDto>>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var unread = query.Unread;
        // Only the caller's own rows; (user_id, is_read) is indexed.
        Expression<Func<Notification, bool>> filter = n =>
            n.UserId == userId && (unread == null || n.IsRead != unread);

        var totalCount = await _unitOfWork.Notifications.CountAsync(filter, cancellationToken);
        var items = await _unitOfWork.Notifications.ListAsync(
            filter,
            orderBy: n => n.CreatedAt,
            descending: true,
            skip: (query.Page - 1) * query.PageSize,
            take: query.PageSize,
            selector: NotificationProjections.ToDto,
            cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        return Result<PaginatedList<NotificationDto>>.Success(
            new PaginatedList<NotificationDto>(items.ToList(), query.Page, query.PageSize, totalCount, totalPages));
    }
}
