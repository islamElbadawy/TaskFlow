using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs;
using TaskFlow.Application.Queries.Users;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.SetUserActiveCommand;

/// <summary>
/// Sets <c>users.is_active</c> (idempotent). A deactivated user can't log in, and the tokens they already hold are
/// rejected on their next request (checked in JwtBearerEvents.OnTokenValidated). The Admin-only rule is on the endpoint.
/// </summary>
public class SetUserActiveCommandHandler : ICommandHandler<SetUserActiveCommand, Result<UserDto>>
{
    public const string CannotDeactivateSelfMessage = "You cannot deactivate your own account";
    public const string LastActiveAdminMessage = "Cannot deactivate the last active Admin";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public SetUserActiveCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<UserDto>> HandleAsync(SetUserActiveCommand command,
        CancellationToken cancellationToken = default)
    {
        if (_currentUserService.UserId is not { } actorId)
            return Result<UserDto>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        // Presence is checked by SetUserActiveCommandValidator.
        var isActive = command.IsActive.GetValueOrDefault();

        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
            return Result<UserDto>.NotFound("User not found");

        if (user.IsActive && !isActive)
        {
            if (user.Id == actorId)
                return Result<UserDto>.Conflict(CannotDeactivateSelfMessage);

            if (await _unitOfWork.Users.IsLastActiveAdminAsync(user, cancellationToken))
                return Result<UserDto>.Conflict(LastActiveAdminMessage);
        }

        if (user.IsActive != isActive)
        {
            user.IsActive = isActive;   // tracked entity: SaveChanges updates is_active and updated_at
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var dto = await _unitOfWork.Users.GetDtoAsync(user.Id, cancellationToken);
        return Result<UserDto>.Success(dto!);
    }
}
