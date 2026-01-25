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
        var user = await _unitOfWork.Users.GetByIdAsync(command.UserId);

        if (user == null)
            return Result<bool>.Failure("User not found");

        // Check if user has created tasks
        var hasCreatedTasks = await _unitOfWork.TasksItems
            .CountAsync(t => t.CreatedByUserId == command.UserId) > 0;

        if (hasCreatedTasks)
            return Result<bool>.Failure("Cannot delete user with created tasks");


        await _unitOfWork.Users.DeleteAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return Result<bool>.Success(true);
    }
}