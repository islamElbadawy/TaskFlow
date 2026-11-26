namespace TaskFlow.Application.DTOs;

public record BoardChangeDto(Guid TaskId, string Action, Guid? ActorId = null);
