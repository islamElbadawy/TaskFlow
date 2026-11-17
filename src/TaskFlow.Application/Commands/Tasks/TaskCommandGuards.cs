using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Commands.Tasks;

/// <summary>Checks shared by the task command handlers.</summary>
internal static class TaskCommandGuards
{
    /// <summary>Null if the user can be assigned a task, otherwise the validation message (unknown or inactive user).</summary>
    public static async Task<string?> CheckAssigneeAsync(this IUnitOfWork unitOfWork, Guid assigneeId,
        CancellationToken cancellationToken)
    {
        var assignee = (await unitOfWork.Users.ListAsync(u => u.Id == assigneeId, u => new { u.IsActive },
            cancellationToken)).SingleOrDefault();

        if (assignee is null)
            return "Assigned user not found";

        return assignee.IsActive ? null : "Assigned user is inactive";
    }
}
