using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.Commands.Users.UpdateUserRoleCommand;

public class UpdateUserRoleCommand : ICommand<Result<bool>>
{
    public int UserId { get; set; }
    public string NewRole { get; set; } = string.Empty;
}
