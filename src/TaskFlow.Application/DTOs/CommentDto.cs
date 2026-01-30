namespace TaskFlow.Application.DTOs;

public record CommentDto(
    int Id,
    string Content,
    int TaskId,
    int UserId,
    string UserName,
    DateTime CreatedAt
);