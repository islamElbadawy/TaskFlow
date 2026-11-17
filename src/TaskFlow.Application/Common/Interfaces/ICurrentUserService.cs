using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }

    /// <summary>The role from the access token, or null when there is no valid role claim.</summary>
    UserRole? Role { get; }
}
