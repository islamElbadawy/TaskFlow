namespace TaskFlow.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Message,
    string Type,
    bool IsRead,
    Guid? RelatedTaskId,
    DateTime CreatedAt
);
