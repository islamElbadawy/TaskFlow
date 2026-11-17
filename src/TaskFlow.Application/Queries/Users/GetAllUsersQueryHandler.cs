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
        // Projected, no-tracking read: only the columns the DTO needs (PasswordHash is never loaded).
        var users = await _unitOfWork.Users.ListAsync(
            filter: null,
            selector: u => new { u.Id, u.FirstName, u.LastName, u.Email, u.Role, u.CreatedAt },
            cancellationToken);

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