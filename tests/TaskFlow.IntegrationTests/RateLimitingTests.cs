using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>Login and register share one fixed-window limit per client IP. The shared test API sets a huge limit,
/// so this test starts its own host (same database) with a limit of 3.</summary>
[Collection(ApiCollection.Name)]
public sealed class RateLimitingTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Login_and_register_get_429_after_the_limit_other_endpoints_do_not()
    {
        await using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:Auth:PermitLimit", "3"));
        var client = limited.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var wrongPassword = new { email = SeedUsers.User, password = "wrong-password" };

        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", wrongPassword)).StatusCode);
        var register = await client.PostAsJsonAsync("/api/auth/register", new { firstName = "", lastName = "", email = "", password = "" });
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);   // the third request of the window

        var rejected = await client.PostAsJsonAsync("/api/auth/login", wrongPassword);
        await rejected.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "TooManyRequests", "Too many attempts. Try again later.");
        Assert.True(rejected.Headers.RetryAfter is not null, "Retry-After header expected");

        var registerRejected = await client.PostAsJsonAsync("/api/auth/register", new { firstName = "A", lastName = "B", email = "a@b.test", password = "123456" });
        Assert.Equal(HttpStatusCode.TooManyRequests, registerRejected.StatusCode);

        // Only login and register are limited.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }
}
