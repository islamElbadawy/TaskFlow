using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Queries.Users;

public class GetUserByIdQuery : IQuery<Result<UserDto>>
{
    public int UserId { get; set; }
}