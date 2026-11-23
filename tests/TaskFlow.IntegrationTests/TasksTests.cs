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
        var alice = await factory.RegisterManagerAsync("AliceScope");
        var bob = await factory.RegisterAsync("BobScope");
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        var aliceOwn = await CreateTaskAsync(alice.Client, "Scope: alice own");
        var aliceToBob = await CreateTaskAsync(alice.Client, "Scope: alice to bob", assignedToId: bob.Id);
        var managerOwn = await CreateTaskAsync(manager, "Scope: manager own");

        // Alice (Manager, non-admin) sees her own tasks (created by her), not another manager's.
        var aliceList = await ListTaskIdsAsync(alice.Client, "search=Scope:");
        Assert.Contains(aliceOwn, aliceList);
        Assert.Contains(aliceToBob, aliceList);
        Assert.DoesNotContain(managerOwn, aliceList);

        // Even with an explicit createdById filter for another user, Alice still cannot see their task.
        var aliceFiltered = await ListTaskIdsAsync(alice.Client, $"createdById={TaskFlow.Infrastructure.Data.Seeding.DataSeeder.UserIds.Manager}");
        Assert.DoesNotContain(managerOwn, aliceFiltered);

        // Bob (User) sees only tasks assigned to him, not Alice's unassigned task or Manager's task.
        var bobList = await ListTaskIdsAsync(bob.Client, "search=Scope:");
        Assert.Contains(aliceToBob, bobList);
        Assert.DoesNotContain(aliceOwn, bobList);
        Assert.DoesNotContain(managerOwn, bobList);

        // Manager (non-admin) sees only their own task, none of Alice's tasks.
        var managerList = await ListTaskIdsAsync(manager, "search=Scope:");
        Assert.Contains(managerOwn, managerList);
        Assert.DoesNotContain(aliceOwn, managerList);
        Assert.DoesNotContain(aliceToBob, managerList);

        // Admin sees all three tasks.
        var adminList = await ListTaskIdsAsync(admin, "search=Scope:");
        Assert.Contains(aliceOwn, adminList);
        Assert.Contains(aliceToBob, adminList);
        Assert.Contains(managerOwn, adminList);
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

    [Fact]
    public async Task Regular_user_cannot_create_a_task_403_while_manager_and_admin_can()
    {
        var user = await factory.LoginAsync(SeedUsers.User);
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        var userResponse = await user.PostAsJsonAsync("/api/tasks",
            new { title = "Test: blocked user create", description = "", priority = "Medium" });
        Assert.Equal(HttpStatusCode.Forbidden, userResponse.StatusCode);

        var managerResponse = await manager.PostAsJsonAsync("/api/tasks",
            new { title = "Test: manager create ok", description = "", priority = "Medium" });
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);

        var adminResponse = await admin.PostAsJsonAsync("/api/tasks",
            new { title = "Test: admin create ok", description = "", priority = "Medium" });
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
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
