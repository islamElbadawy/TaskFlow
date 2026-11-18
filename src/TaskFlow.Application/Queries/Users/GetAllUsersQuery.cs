using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Queries.Users;

/// <summary>GET /api/users, optionally <c>?isActive=true|false</c>.</summary>
public class GetAllUsersQuery : IQuery<Result<List<UserDto>>>
{
    /// <summary>true: only active accounts; false: only deactivated ones; omitted: all.</summary>
    public bool? IsActive { get; set; }
}
