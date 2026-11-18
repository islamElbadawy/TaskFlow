using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users;

/// <summary>Checks shared by the user command handlers.</summary>
internal static class UserCommandGuards
{
    /// <summary>True when <paramref name="user"/> is the only active Admin, so demoting, deactivating or deleting them
    /// would leave nobody able to manage roles and accounts. Deactivated Admins don't count: they can't log in.</summary>
    public static async Task<bool> IsLastActiveAdminAsync(this IRepository<User> users, User user,
        CancellationToken cancellationToken)
    {
        if (user.Role != UserRole.Admin || !user.IsActive)
            return false;

        var activeAdmins = await users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive, cancellationToken);
        return activeAdmins <= 1;
    }
}
