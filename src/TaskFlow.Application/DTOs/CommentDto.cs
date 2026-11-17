namespace TaskFlow.Application.DTOs;

public record CommentDto(
    Guid Id,
    string Content,
    Guid TaskId,
    Guid UserId,
    string UserName,
    DateTime CreatedAt
);
