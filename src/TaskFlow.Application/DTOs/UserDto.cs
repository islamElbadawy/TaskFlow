namespace TaskFlow.Application.DTOs;

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt
);