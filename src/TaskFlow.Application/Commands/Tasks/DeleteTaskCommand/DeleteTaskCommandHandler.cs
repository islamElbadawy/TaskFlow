using TaskFlow.Application.Common;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>Creator or Admin may delete a task. Its comments are deleted by the database (cascade) and its
/// notifications keep existing with the task link cleared (SET NULL).</summary>
public class DeleteTaskCommandHandler : ICommandHandler<DeleteTaskCommand, Result<bool>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DeleteTaskCommandHandler(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<Result<bool>> HandleAsync(DeleteTaskCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUserService.TryGetActor(out var actorId, out var actorRole))
            return Result<bool>.Unauthorized(CurrentUserExtensions.NoActorMessage);

        var task = await _unitOfWork.TasksItems.GetByIdAsync(command.TaskId, cancellationToken);
        if (task == null)
            return Result<bool>.NotFound("Task not found");

        if (!task.CanBeDeletedBy(actorId, actorRole))
            return Result<bool>.Forbidden("Only the task's creator or an Admin can delete this task.");

        await _unitOfWork.TasksItems.DeleteAsync(task, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
