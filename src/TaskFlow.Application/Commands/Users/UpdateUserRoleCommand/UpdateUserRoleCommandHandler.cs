using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.UpdateUserRoleCommand;

/// <summary>Admin changes a user's role. The user's existing tokens are revoked, so the new role (higher or lower)
/// applies from their next login and an old token can't keep using the previous one.</summary>
public class UpdateUserRoleCommandHandler : ICommandHandler<UpdateUserRoleCommand, Result<bool>>
{
    public const string LastActiveAdminMessage = "Cannot demote the last active Admin";

    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserRoleCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> HandleAsync(UpdateUserRoleCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(command.NewRole, true, out var newRole) || !Enum.IsDefined(newRole))
            return Result<bool>.Validation("Invalid role");

        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<bool>.NotFound("User not found");

        if (user.Role == newRole)
            return Result<bool>.Success(true);   // nothing changes, so nobody is signed out

        if (newRole != UserRole.Admin && await _unitOfWork.Users.IsLastActiveAdminAsync(user, cancellationToken))
            return Result<bool>.Conflict(LastActiveAdminMessage);

        user.Role = newRole;
        user.RevokeTokens();

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
