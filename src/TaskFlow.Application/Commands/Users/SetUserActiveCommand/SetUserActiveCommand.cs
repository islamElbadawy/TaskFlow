using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;

namespace TaskFlow.Application.Commands.Users.SetUserActiveCommand;

/// <summary>PATCH /api/users/{id}/active <c>{ "isActive": false }</c>: Admin activates or deactivates an account.</summary>
public class SetUserActiveCommand : ICommand<Result<UserDto>>
{
    /// <summary>From the route (set by the controller).</summary>
    public Guid UserId { get; set; }

    /// <summary>Required. Nullable so a missing value is a 400 instead of silently meaning false.</summary>
    public bool? IsActive { get; set; }
}
