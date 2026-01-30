using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;
using TaskFlow.Application.DTOs.Auth;

namespace TaskFlow.Application.Commands.Users.LoginCommand;

public class LoginCommand: ICommand<Result<LoginResponse>>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

}