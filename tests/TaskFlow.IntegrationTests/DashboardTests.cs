using System.Net;
using System.Net.Http.Json;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>GET /api/dashboard. The "my ..." parts use freshly registered users, so they are exact; the all-task
/// counts are shared with other tests, so they are checked against /api/tasks and as differences.</summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Requires_a_token()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task New_user_has_an_empty_personal_section()
    {
        var user = await factory.RegisterAsync("Nora");

        var dashboard = await DashboardAsync(user.Client);

        Assert.Equal(new StatusCounts(0, 0, 0, 0), dashboard.MyAssignedTasks);
        Assert.Equal(0, dashboard.MyOpenCreatedTasks);
        Assert.Equal(0, dashboard.UnreadNotifications);
        Assert.Empty(dashboard.RecentTasks);
    }

    [Fact]
    public async Task All_task_counts_match_the_task_list()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var dashboard = await DashboardAsync(client);

        Assert.Equal(await TaskCountAsync(client, ""), dashboard.Tasks.Total);
        Assert.Equal(await TaskCountAsync(client, "status=Todo"), dashboard.Tasks.Todo);
        Assert.Equal(await TaskCountAsync(client, "status=InProgress"), dashboard.Tasks.InProgress);
        Assert.Equal(await TaskCountAsync(client, "status=Done"), dashboard.Tasks.Done);
        Assert.Equal(dashboard.Tasks.Todo + dashboard.Tasks.InProgress + dashboard.Tasks.Done, dashboard.Tasks.Total);
        Assert.Equal(await TaskCountAsync(client, "overdue=true"), dashboard.OverdueTasks);
        Assert.True(dashboard.DueSoonTasks >= 2);   // seed: "Write docs" (+3 days), "Review sprint backlog" (+1 day)
    }

    [Fact]
    public async Task Seeded_overdue_task_is_counted()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var dashboard = await DashboardAsync(client);

        // Seed: "Fix password reset email" is due 2 days ago and Todo; the other past-due seed tasks are Done.
        var overdue = await ReadAsync<Page>(await client.GetAsync("/api/tasks?overdue=true&pageSize=100"));
        Assert.Contains(overdue.Items, t => t.Title == "Fix password reset email" && t.IsOverdue);
        Assert.Equal(overdue.TotalCount, dashboard.OverdueTasks);
        // The seeded user is its assignee (Todo) and the assignee of "Write docs" (InProgress).
        Assert.True(dashboard.MyAssignedTasks.Todo >= 1);
        Assert.True(dashboard.MyAssignedTasks.InProgress >= 1);
    }

    [Fact]
    public async Task Overdue_and_due_soon_count_only_open_tasks_in_their_window()
    {
        var user = await factory.RegisterAsync("Owen");
        var before = await DashboardAsync(user.Client);

        await CreateTaskAsync(user, "Test: overdue", due: DateTime.UtcNow.AddDays(-1));
        await CreateTaskAsync(user, "Test: due in 3 days", due: DateTime.UtcNow.AddDays(3));
        await CreateTaskAsync(user, "Test: due in 10 days", due: DateTime.UtcNow.AddDays(10));
        var doneLate = await CreateTaskAsync(user, "Test: done and late", due: DateTime.UtcNow.AddDays(-1));
        var doneSoon = await CreateTaskAsync(user, "Test: done and due soon", due: DateTime.UtcNow.AddDays(2));
        await PatchStatusAsync(user, doneLate, "Done");
        await PatchStatusAsync(user, doneSoon, "Done");

        var after = await DashboardAsync(user.Client);
        Assert.Equal(before.OverdueTasks + 1, after.OverdueTasks);
        Assert.Equal(before.DueSoonTasks + 1, after.DueSoonTasks);
        Assert.Equal(before.Tasks.Total + 5, after.Tasks.Total);
        Assert.Equal(before.Tasks.Todo + 3, after.Tasks.Todo);
        Assert.Equal(before.Tasks.Done + 2, after.Tasks.Done);
        Assert.Equal(3, after.MyOpenCreatedTasks);
    }

    [Fact]
    public async Task Personal_section_counts_assigned_created_and_unread()
    {
        var alice = await factory.RegisterAsync("Alma");
        var bob = await factory.RegisterAsync("Boris");

        var mine = await CreateTaskAsync(alice, "Test: Alma's own", assignedToId: alice.Id);
        var forBob = await CreateTaskAsync(alice, "Test: for Boris", assignedToId: bob.Id);
        var finished = await CreateTaskAsync(alice, "Test: finished");
        await PatchStatusAsync(alice, finished, "Done");
        await PatchStatusAsync(bob, forBob, "InProgress");

        var alices = await DashboardAsync(alice.Client);
        Assert.Equal(new StatusCounts(1, 1, 0, 0), alices.MyAssignedTasks);
        Assert.Equal(2, alices.MyOpenCreatedTasks);
        Assert.Equal(0, alices.UnreadNotifications);   // she did everything herself; Boris's status change isn't Done
        Assert.Equal(new[] { forBob, finished, mine }, alices.RecentTasks.Select(t => t.Id));   // most recently changed first

        var bobs = await DashboardAsync(bob.Client);
        Assert.Equal(new StatusCounts(1, 0, 1, 0), bobs.MyAssignedTasks);
        Assert.Equal(0, bobs.MyOpenCreatedTasks);
        Assert.Equal(1, bobs.UnreadNotifications);     // TaskAssigned
        var recent = Assert.Single(bobs.RecentTasks);
        Assert.Equal(forBob, recent.Id);
        Assert.Equal("InProgress", recent.Status);
        Assert.Equal("Alma Tester", recent.CreatedByName);
        Assert.Equal("Boris Tester", recent.AssignedToName);
    }

    [Fact]
    public async Task Recent_tasks_are_the_five_most_recently_updated()
    {
        var user = await factory.RegisterAsync("Rita");
        var ids = new List<Guid>();
        for (var i = 1; i <= 6; i++)
            ids.Add(await CreateTaskAsync(user, $"Test: recent {i}"));
        await PatchStatusAsync(user, ids[0], "InProgress");   // the oldest task becomes the most recent

        var dashboard = await DashboardAsync(user.Client);

        Assert.Equal(new[] { ids[0], ids[5], ids[4], ids[3], ids[2] }, dashboard.RecentTasks.Select(t => t.Id));
        Assert.Equal(6, dashboard.MyOpenCreatedTasks);
    }

    // ---- helpers ----

    private sealed record StatusCounts(int Total, int Todo, int InProgress, int Done);

    private sealed record RecentTask(Guid Id, string Title, string Status, bool IsOverdue, string CreatedByName,
        string? AssignedToName, DateTime CreatedAt, DateTime? UpdatedAt);

    private sealed record Dashboard(StatusCounts Tasks, int OverdueTasks, int DueSoonTasks, StatusCounts MyAssignedTasks,
        int MyOpenCreatedTasks, int UnreadNotifications, List<RecentTask> RecentTasks);

    private sealed record Page(int TotalCount, List<RecentTask> Items);

    private sealed record IdOnly(Guid Id);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }

    private static async Task<Dashboard> DashboardAsync(HttpClient client) =>
        await ReadAsync<Dashboard>(await client.GetAsync("/api/dashboard"));

    private static async Task<int> TaskCountAsync(HttpClient client, string filter) =>
        (await ReadAsync<Page>(await client.GetAsync($"/api/tasks?pageSize=1&{filter}"))).TotalCount;

    private static async Task<Guid> CreateTaskAsync(TestUser creator, string title, Guid? assignedToId = null,
        DateTime? due = null) =>
        (await ReadAsync<IdOnly>(await creator.Client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", dueDate = due, assignedToId }))).Id;

    private static async Task PatchStatusAsync(TestUser user, Guid taskId, string status) =>
        await ReadAsync<IdOnly>(await user.Client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new { status }));
}
