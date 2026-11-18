using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TaskFlow.API.Common;

/// <summary>
/// Rate limits. <see cref="AuthPolicy"/> protects login, register and change-password against password guessing and mass sign-ups:
/// a fixed window per client IP, sized by <c>RateLimiting:Auth:PermitLimit</c> (default 10) requests per
/// <c>RateLimiting:Auth:WindowSeconds</c> (default 60). A rejected request gets 429 with a Retry-After header and a
/// ProblemDetails body. Behind a reverse proxy, configure forwarded headers so the client IP is the real one.
/// </summary>
public static class RateLimiting
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddTaskFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("RateLimiting:Auth");
        var permitLimit = section.GetValue("PermitLimit", 10);
        var window = TimeSpan.FromSeconds(section.GetValue("WindowSeconds", 60));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "TooManyRequests",
                        Detail = "Too many attempts. Try again later."
                    }
                });
            };
        });
    }
}
