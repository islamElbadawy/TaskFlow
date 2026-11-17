using System.Net;
using System.Net.Http.Json;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TasksTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Unknown_task_is_404_problem_details()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var response = await client.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
    }

    [Fact]
    public async Task Creating_a_task_with_an_unknown_priority_is_400_not_a_silent_default()
    {
        var client = await factory.LoginAsync(SeedUsers.Manager);

        var response = await client.PostAsJsonAsync("/api/tasks",
            new { title = "Test: invalid priority", description = "", priority = "Urgent" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Priority", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task Creating_a_task_with_an_unknown_assignee_is_400()
    {
        var client = await factory.LoginAsync(SeedUsers.Manager);

        var response = await client.PostAsJsonAsync("/api/tasks",
            new { title = "Test: unknown assignee", description = "", priority = "High", assignedToId = Guid.NewGuid() });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "Assigned user not found");
    }

    [Fact]
    public async Task Creating_a_task_sets_the_caller_as_creator()
    {
        var client = await factory.LoginAsync(SeedUsers.Manager);

        var response = await client.PostAsJsonAsync("/api/tasks",
            new { title = "Test: created by manager", description = "", priority = "low" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<CreatedTask>>();
        Assert.Equal(TaskFlow.Infrastructure.Data.Seeding.DataSeeder.UserIds.Manager, body!.Data!.CreatedById);
        Assert.Equal("Low", body.Data.Priority);
    }

    private sealed record CreatedTask(Guid Id, string Title, string Priority, Guid CreatedById);
}
