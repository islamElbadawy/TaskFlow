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
        var users = await _unitOfWork.Users.GetAllAsync();

        var userDtos = users.Select(u => new UserDto(
            Id: u.Id,
            FirstName: u.FirstName,
            LastName: u.LastName,
            FullName: $"{u.FirstName} {u.LastName}",
            Email: u.Email,
            Role: u.Role.ToString(),
            CreatedAt: u.CreatedAt
        )).ToList();

        return Result<List<UserDto>>.Success(userDtos);
    }
}