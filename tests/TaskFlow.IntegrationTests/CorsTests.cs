using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>CORS comes from Cors:AllowedOrigins (the test API runs as Development, so it uses the local front-end
/// origins from appsettings.Development.json).</summary>
[Collection(ApiCollection.Name)]
public sealed class CorsTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Configured_origin_may_call_the_api_with_credentials()
    {
        var response = await PreflightAsync("http://localhost:5173");

        Assert.Equal("http://localhost:5173", Header(response, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Other_origins_are_not_allowed()
    {
        var response = await PreflightAsync("https://evil.example");

        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
    }

    private Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/tasks");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return factory.CreateAnonymousClient().SendAsync(request);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
