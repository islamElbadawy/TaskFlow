namespace TaskFlow.Application.DTOs;

/// <summary>A user account. <see cref="IsActive"/> false: deactivated by an Admin, can't log in or use old tokens.</summary>
public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);
