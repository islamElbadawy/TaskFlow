using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Users;

public class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, Result<UserDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserByIdQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserDto>> HandleAsync(GetUserByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetDtoAsync(query.UserId, cancellationToken);
        return user is null
            ? Result<UserDto>.NotFound("User not found")
            : Result<UserDto>.Success(user);
    }
}
