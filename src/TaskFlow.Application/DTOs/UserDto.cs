namespace TaskFlow.Application.DTOs;

public record UserDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt
);