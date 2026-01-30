namespace TaskFlow.Application.DTOs;

public record TaskDto(
    int Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    DateTime? DueDate,
    int CreatedById,
    string CreatedByName,
    int? AssignedToId,
    string? AssignedToName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int CommentsCount
);