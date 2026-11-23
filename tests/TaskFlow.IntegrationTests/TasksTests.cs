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

    [Fact]
    public async Task Non_admins_only_see_tasks_they_created_or_are_assigned_to_while_admin_sees_all()
    {
        var alice = await factory.RegisterAsync("AliceScope");
        var bob = await factory.RegisterAsync("BobScope");
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        var aliceOwn = await CreateTaskAsync(alice.Client, "Scope: alice own");
        var aliceToBob = await CreateTaskAsync(alice.Client, "Scope: alice to bob", assignedToId: bob.Id);
        var bobOwn = await CreateTaskAsync(bob.Client, "Scope: bob own");

        // Alice sees her own tasks (created by her), not Bob's.
        var aliceList = await ListTaskIdsAsync(alice.Client, "search=Scope:");
        Assert.Contains(aliceOwn, aliceList);
        Assert.Contains(aliceToBob, aliceList);
        Assert.DoesNotContain(bobOwn, aliceList);

        // Even with an explicit createdById filter for Bob, Alice still cannot see Bob's task.
        var aliceFiltered = await ListTaskIdsAsync(alice.Client, $"createdById={bob.Id}");
        Assert.DoesNotContain(bobOwn, aliceFiltered);

        // Bob sees tasks assigned to him or created by him, not Alice's unassigned task.
        var bobList = await ListTaskIdsAsync(bob.Client, "search=Scope:");
        Assert.Contains(aliceToBob, bobList);
        Assert.Contains(bobOwn, bobList);
        Assert.DoesNotContain(aliceOwn, bobList);

        // Manager (non-admin) sees none of Alice's or Bob's tasks.
        var managerList = await ListTaskIdsAsync(manager, "search=Scope:");
        Assert.DoesNotContain(aliceOwn, managerList);
        Assert.DoesNotContain(aliceToBob, managerList);
        Assert.DoesNotContain(bobOwn, managerList);

        // Admin sees all three tasks.
        var adminList = await ListTaskIdsAsync(admin, "search=Scope:");
        Assert.Contains(aliceOwn, adminList);
        Assert.Contains(aliceToBob, adminList);
        Assert.Contains(bobOwn, adminList);
    }

    [Fact]
    public async Task Non_admins_cannot_filter_tasks_by_assignee_403_while_admin_can()
    {
        var user = await factory.LoginAsync(SeedUsers.User);
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var targetId = TaskFlow.Infrastructure.Data.Seeding.DataSeeder.UserIds.User;

        foreach (var client in new[] { user, manager })
        {
            await (await client.GetAsync($"/api/tasks?assignedToId={targetId}"))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden", "Only Admins can filter tasks by assignee.");
            await (await client.GetAsync("/api/tasks?unassigned=true"))
                .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden", "Only Admins can filter tasks by assignee.");
        }

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tasks?assignedToId={targetId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/tasks?unassigned=true")).StatusCode);
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient client, string title, Guid? assignedToId = null)
    {
        var response = await client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", assignedToId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<CreatedTask>>())!.Data!.Id;
    }

    private static async Task<HashSet<Guid>> ListTaskIdsAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/api/tasks?pageSize=100&{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = (await response.Content.ReadFromJsonAsync<ApiResult<TaskPage>>())!.Data!;
        return page.Items.Select(t => t.Id).ToHashSet();
    }

    private sealed record CreatedTask(Guid Id, string Title, string Priority, Guid CreatedById);
    private sealed record TaskPage(List<TaskItemId> Items);
    private sealed record TaskItemId(Guid Id);
}
