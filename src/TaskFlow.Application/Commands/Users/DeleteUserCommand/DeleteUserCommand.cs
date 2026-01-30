using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.Commands.Users.DeleteUserCommand;

public class DeleteUserCommand : ICommand<Result<bool>>
{
    public int UserId { get; set; }
}