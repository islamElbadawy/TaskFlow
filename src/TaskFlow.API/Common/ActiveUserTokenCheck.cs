using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TaskFlow.Application.Common;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.API.Common;

/// <summary>
/// Runs after a JWT's signature and lifetime are valid (JwtBearerEvents.OnTokenValidated): the token's user must still
/// exist, be active, and have the same security stamp as when the token was issued, otherwise the request is 401.
/// This makes deactivation, deletion, password changes and role changes effective immediately instead of when the
/// token expires (a role change rotates the stamp, so the role claim in an accepted token always matches the database).
/// Cost: one primary-key lookup per authenticated request, including the SignalR negotiate/connect requests (an
/// already open SignalR connection is not closed).
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

        // Tokens issued before the stamp existed have no stamp claim and are rejected: those users log in again.
        if (!Guid.TryParse(context.Principal?.FindFirstValue(TaskFlowClaims.SecurityStamp), out var securityStamp))
        {
            context.Fail("The access token has no security stamp.");
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>().Users;
        var isValid = await users.AnyAsync(u => u.Id == userId && u.IsActive && u.SecurityStamp == securityStamp,
            context.HttpContext.RequestAborted);
        if (!isValid)
            context.Fail("The user account is inactive, no longer exists, or its access tokens were revoked.");
    }
}
