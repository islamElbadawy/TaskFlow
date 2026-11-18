using System.Security.Claims;
using TaskFlow.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var userIdClaims = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(userIdClaims, out var userId))
                return userId;

            return null;
        }
    }

    public UserRole? Role =>
        Enum.TryParse<UserRole>(User?.FindFirstValue(ClaimTypes.Role), ignoreCase: false, out var role) && Enum.IsDefined(role)
            ? role
            : null;
}
