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
        var user = await _unitOfWork.Users.GetByIdAsync(query.UserId);


        if (user == null)
            return Result<UserDto>.Failure("User not found");

        var userDto = new UserDto
        (
            Id: user.Id,
            FirstName: user.FirstName,
            LastName: user.LastName,
            FullName: $"{user.FirstName} {user.LastName}",
            Email: user.Email,
            Role: user.Role.ToString(),
            CreatedAt: user.CreatedAt
        );
        return Result<UserDto>.Success(userDto);
    }
}