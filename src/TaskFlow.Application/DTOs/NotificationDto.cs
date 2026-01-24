namespace TaskFlow.Application.DTOs;

public record NotificationDto(
    int Id,
    string Message,
    string Type,
    bool IsRead,
    int? RelatedTaskId,
    DateTime CreatedAt
);