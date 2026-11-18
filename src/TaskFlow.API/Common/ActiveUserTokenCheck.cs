using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.API.Common;

/// <summary>
/// Runs after a JWT's signature and lifetime are valid (JwtBearerEvents.OnTokenValidated): the token's user must still
/// exist and be active, otherwise the request is 401. This makes deactivation (and deletion) effective immediately
/// instead of when the token expires. Cost: one primary-key lookup of users.is_active per authenticated request,
/// including the SignalR negotiate/connect requests (an already open SignalR connection is not closed).
/// </summary>
public static class ActiveUserTokenCheck
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Fail("The access token does not identify a user.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>().Users;
        var isActive = await users.AnyAsync(u => u.Id == userId && u.IsActive, context.HttpContext.RequestAborted);
        if (!isActive)
            context.Fail("The user account is inactive or no longer exists.");
    }
}
