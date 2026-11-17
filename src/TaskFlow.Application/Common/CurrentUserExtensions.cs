using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common;

public static class CurrentUserExtensions
{
    public const string NoActorMessage = "The access token does not identify a user.";

    /// <summary>The caller's id and role from the token; false when either is missing.</summary>
    public static bool TryGetActor(this ICurrentUserService currentUser, out Guid userId, out UserRole role)
    {
        userId = currentUser.UserId ?? Guid.Empty;
        role = currentUser.Role ?? default;
        return currentUser.UserId.HasValue && currentUser.Role.HasValue;
    }
}
