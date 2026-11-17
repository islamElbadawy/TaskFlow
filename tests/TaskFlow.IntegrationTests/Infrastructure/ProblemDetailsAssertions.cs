using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.IntegrationTests.Infrastructure;

public static class ProblemDetailsAssertions
{
    /// <summary>Asserts an RFC 9457 error response and returns its body.</summary>
    public static async Task<ProblemDetails> ShouldBeProblemAsync(
        this HttpResponseMessage response, HttpStatusCode status, string? title = null, string? detail = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)status, problem.Status);
        if (title is not null)
            Assert.Equal(title, problem.Title);
        if (detail is not null)
            Assert.Equal(detail, problem.Detail);
        return problem;
    }
}
