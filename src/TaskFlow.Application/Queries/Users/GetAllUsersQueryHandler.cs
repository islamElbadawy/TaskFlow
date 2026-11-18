using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Users;

public class GetAllUsersQueryHandler : IQueryHandler<GetAllUsersQuery, Result<List<UserDto>>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetAllUsersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<List<UserDto>>> HandleAsync(GetAllUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        var isActive = query.IsActive;

        // Projected, no-tracking read: only the columns the DTO needs (PasswordHash is never loaded).
        var users = await _unitOfWork.Users.ListAsync(
            filter: u => isActive == null || u.IsActive == isActive,
            selector: UserProjections.ToDto,
            cancellationToken);

        return Result<List<UserDto>>.Success(users.ToList());
    }
}
