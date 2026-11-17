using System;
using System.Collections.Generic;
using System.Text;

namespace TaskFlow.Application.DTOs.Auth;

public record ChangePasswordRequest
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
