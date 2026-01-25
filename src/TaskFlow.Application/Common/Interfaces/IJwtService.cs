using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    int? ValidateToken(string token);
}