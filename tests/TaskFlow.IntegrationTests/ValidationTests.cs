using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>The shape of 400 responses: validator failures (run by the dispatchers) and requests MVC can't bind.</summary>
[Collection(ApiCollection.Name)]
public sealed class ValidationTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Validator_failure_is_a_validation_problem_with_field_errors()
    {
        var user = await factory.RegisterAsync("Vera");

        var response = await user.Client.PostAsJsonAsync("/api/tasks",
            new { title = "Test: bad priority", description = "", priority = "Urgent" });

        var errors = await ErrorsAsync(response, "One or more validation errors occurred.");
        Assert.True(errors.TryGetProperty("Priority", out var messages));
        Assert.Equal(JsonValueKind.Array, messages.ValueKind);
    }

    [Fact]
    public async Task Change_password_validates_the_new_password()
    {
        // ChangePasswordCommandValidator only runs since validation moved into the dispatcher: the controller builds
        // that command itself, so the old automatic model validation never saw it.
        var user = await factory.RegisterAsync("Walt");

        var response = await user.Client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = TestUser.Password, newPassword = "123" });

        var errors = await ErrorsAsync(response, "One or more validation errors occurred.");
        Assert.True(errors.TryGetProperty("NewPassword", out _));
    }

    [Fact]
    public async Task Malformed_json_gets_the_same_validation_shape()
    {
        var user = await factory.RegisterAsync("Xena");

        var response = await user.Client.PostAsync("/api/tasks",
            new StringContent("{ \"title\": ", Encoding.UTF8, "application/json"));

        await ErrorsAsync(response, "One or more validation errors occurred.");
    }

    private static async Task<JsonElement> ErrorsAsync(HttpResponseMessage response, string detail)
    {
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", detail);
        var errors = (JsonElement)problem.Extensions["errors"]!;
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        return errors;
    }
}
