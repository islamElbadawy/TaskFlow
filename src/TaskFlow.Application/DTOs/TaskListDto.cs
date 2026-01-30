namespace TaskFlow.Application.DTOs;

public record TaskListDto(
    int Id,
    string Title,
    string Status,
    string Priority,
    DateTime? DueDate,
    string? AssignedToName,
    DateTime CreatedAt
);