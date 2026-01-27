using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Queries.Users;

public class GetAllUsersQuery : IQuery<Result<List<UserDto>>>
{
}