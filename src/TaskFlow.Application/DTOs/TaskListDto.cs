namespace TaskFlow.Application.DTOs;

/// <summary>A task as shown in lists: everything except the (potentially long) description.</summary>
public record TaskListDto(
    Guid Id,
    string Title,
    string Status,
    int Position,
    string Priority,
    DateTime? DueDate,
    bool IsOverdue,
    Guid CreatedById,
    string CreatedByName,
    Guid? AssignedToId,
    string? AssignedToName,
    int CommentsCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
