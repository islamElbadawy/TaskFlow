using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Results;

namespace TaskFlow.Application.Commands.Users.auth;

public class ChangePassowrdCommand: ICommand<Result<bool>>
{
    public int UserId { get; set; }
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
