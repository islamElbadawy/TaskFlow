using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Users.DeleteUserCommand;

public class DeleteUserCommandHandler : ICommandHandler<DeleteUserCommand, Result<bool>>
{
    private IUnitOfWork _unitOfWork;

    public DeleteUserCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId, cancellationToken);

        if (user == null)
            return Result<bool>.NotFound("User not found");

        // Check if user has created tasks
        var hasCreatedTasks = await _unitOfWork.TasksItems
            .AnyAsync(t => t.CreatedByUserId == command.UserId, cancellationToken);

        if (hasCreatedTasks)
            return Result<bool>.Conflict("Cannot delete user with created tasks");


        await _unitOfWork.Users.DeleteAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}