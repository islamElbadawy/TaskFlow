using System.Linq.Expressions;
using TaskFlow.Application.DTOs;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Queries.Users;

public static class UserProjections
{
    /// <summary>A user as returned by the API, translated to SQL (the password hash is never read).</summary>
    public static readonly Expression<Func<User, UserDto>> ToDto = u => new UserDto(
        u.Id,
        u.FirstName,
        u.LastName,
        u.FirstName + " " + u.LastName,
        u.Email,
        u.Role.ToString(),
        u.IsActive,
        u.CreatedAt);

    /// <summary>Projected, no-tracking read of one user, or null if it doesn't exist.</summary>
    public static async Task<UserDto?> GetDtoAsync(this IRepository<User> users, Guid userId,
        CancellationToken cancellationToken) =>
        (await users.ListAsync(u => u.Id == userId, ToDto, cancellationToken)).SingleOrDefault();
}
