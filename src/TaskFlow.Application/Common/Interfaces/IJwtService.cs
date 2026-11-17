using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IJwtService
{
    /// <summary>Creates a signed access token and returns it with its UTC expiry.</summary>
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
    Guid? ValidateToken(string token);
}