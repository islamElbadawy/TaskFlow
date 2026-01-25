namespace TaskFlow.Application.Common.Interfaces;

public class ICurrentUserService
{
    int? UserId { get; }
    string? UserEmail { get; }
    bool IsAuthenticated { get; }
}