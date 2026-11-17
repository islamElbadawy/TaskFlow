using System;
using System.Threading;
using System.Threading.Tasks;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Enums;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.UpdateUserRoleCommand;

public class UpdateUserRoleCommandHandler : ICommandHandler<UpdateUserRoleCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserRoleCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> HandleAsync(UpdateUserRoleCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(command.NewRole, true, out var newRole))
            return Result<bool>.Validation("Invalid role");

        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);
        if (user == null)
            return Result<bool>.NotFound("User not found");

        // Prevent demoting the last remaining Admin
        if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
        {
            var adminCount = await _unitOfWork.Users.CountAsync(u => u.Role == UserRole.Admin, cancellationToken);
            if (adminCount <= 1)
                return Result<bool>.Conflict("Cannot demote the last Admin");
        }

        user.Role = newRole;

        await _unitOfWork.Users.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
