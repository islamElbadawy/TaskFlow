using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>Automatic notifications, the read API and the SignalR push. Each test registers its own users,
/// so notification counts are exact.</summary>
[Collection(ApiCollection.Name)]
public sealed class NotificationsTests(TaskFlowApiFactory factory)
{
    // ---- automatic creation ----

    [Fact]
    public async Task Assigning_creates_a_notification_for_the_assignee_not_the_actor()
    {
        var creator = await factory.RegisterManagerAsync("Alice");
        var assignee = await factory.RegisterAsync("Bob");

        var taskId = await CreateTaskAsync(creator, "Test: assigned to Bob", assignee.Id);

        var bobs = await NotificationsAsync(assignee);
        var n = Assert.Single(bobs);
        Assert.Equal("TaskAssigned", n.Type);
        Assert.Equal(taskId, n.RelatedTaskId);
        Assert.Equal("Alice Tester assigned you \"Test: assigned to Bob\"", n.Message);
        Assert.False(n.IsRead);
        Assert.Empty(await NotificationsAsync(creator));
    }

    [Fact]
    public async Task Reassigning_notifies_the_new_assignee_only()
    {
        var creator = await factory.RegisterManagerAsync("Carol");
        var first = await factory.RegisterAsync("Dan");
        var second = await factory.RegisterAsync("Erin");
        var taskId = await CreateTaskAsync(creator, "Test: reassign", first.Id);

        var response = await creator.Client.PatchAsJsonAsync($"/api/tasks/{taskId}/assignee", new { assignedToId = second.Id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("TaskAssigned", Assert.Single(await NotificationsAsync(second)).Type);
        Assert.Single(await NotificationsAsync(first));
        Assert.Empty(await NotificationsAsync(creator));
    }

    [Fact]
    public async Task Comment_notifies_creator_and_assignee_but_never_the_author()
    {
        var creator = await factory.RegisterManagerAsync("Frank");
        var assignee = await factory.RegisterAsync("Grace");
        var adminClient = await factory.LoginAsync(SeedUsers.Admin);
        var taskId = await CreateTaskAsync(creator, "Test: commented", assignee.Id);

        Assert.Equal(HttpStatusCode.OK,
            (await adminClient.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = "From an admin" })).StatusCode);
        await CommentAsync(assignee, taskId, "From the assignee");

        var creators = await NotificationsAsync(creator);
        Assert.Equal(2, creators.Count);
        Assert.All(creators, n => Assert.Equal("CommentAdded", n.Type));
        Assert.Contains(creators, n => n.Message == "Grace Tester commented on \"Test: commented\"");

        var assignees = await NotificationsAsync(assignee);
        Assert.Equal(new[] { "CommentAdded", "TaskAssigned" }, assignees.Select(n => n.Type));   // newest first; not their own comment
    }

    [Fact]
    public async Task Status_done_notifies_the_creator_and_status_changes_notify_the_assignee()
    {
        var creator = await factory.RegisterManagerAsync("Ivan");
        var assignee = await factory.RegisterAsync("Judy");
        var taskId = await CreateTaskAsync(creator, "Test: completed", assignee.Id);

        // The assignee finishes the task: the creator hears about it, the assignee (actor) gets nothing new.
        await PatchStatusAsync(assignee, taskId, "Done");
        var creators = await NotificationsAsync(creator);
        Assert.Equal("TaskCompleted", Assert.Single(creators).Type);
        Assert.Equal("Judy Tester marked \"Test: completed\" as done", creators[0].Message);
        Assert.Single(await NotificationsAsync(assignee));

        // The creator reopens it: the assignee is told about the status change.
        await PatchStatusAsync(creator, taskId, "InProgress");
        var latest = (await NotificationsAsync(assignee))[0];
        Assert.Equal("TaskUpdated", latest.Type);
        Assert.Equal("Ivan Tester moved \"Test: completed\" to InProgress", latest.Message);
    }

    [Fact]
    public async Task One_notification_per_user_per_change_and_none_for_a_no_op_update()
    {
        var owner = await factory.RegisterManagerAsync("Kim");
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        // Kim creates a task for herself: creator and assignee are the same person.
        var taskId = await CreateTaskAsync(owner, "Test: dedupe", owner.Id);
        Assert.Empty(await NotificationsAsync(owner));

        // A Manager completes it with PUT (status + title): Kim is creator (TaskCompleted) and assignee (TaskUpdated),
        // but gets a single notification.
        var put = await manager.PutAsJsonAsync($"/api/tasks/{taskId}", new
        {
            title = "Test: dedupe (done)", description = "", priority = "Medium", status = "Done", dueDate = (DateTime?)null,
            assignedToId = owner.Id
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("TaskCompleted", Assert.Single(await NotificationsAsync(owner)).Type);

        // Sending the same values again changes nothing, so nobody is notified.
        var same = await manager.PutAsJsonAsync($"/api/tasks/{taskId}", new
        {
            title = "Test: dedupe (done)", description = "", priority = "Medium", status = "Done", dueDate = (DateTime?)null,
            assignedToId = owner.Id
        });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        Assert.Single(await NotificationsAsync(owner));
    }

    [Fact]
    public async Task Update_by_someone_else_notifies_the_assignee()
    {
        var creator = await factory.RegisterManagerAsync("Leo");
        var assignee = await factory.RegisterAsync("Mia");
        var taskId = await CreateTaskAsync(creator, "Test: updated", assignee.Id);

        var put = await creator.Client.PutAsJsonAsync($"/api/tasks/{taskId}", new
        {
            title = "Test: updated (new title)", description = "more", priority = "High", status = "Todo",
            dueDate = (DateTime?)null, assignedToId = assignee.Id
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var latest = (await NotificationsAsync(assignee))[0];
        Assert.Equal("TaskUpdated", latest.Type);
        Assert.Equal("Leo Tester updated \"Test: updated (new title)\"", latest.Message);
        Assert.Empty(await NotificationsAsync(creator));
    }

    // ---- read API ----

    [Fact]
    public async Task Read_api_lists_counts_filters_and_marks_only_your_own()
    {
        var creator = await factory.RegisterManagerAsync("Nina");
        var me = await factory.RegisterAsync("Omar");
        var someoneElse = await factory.RegisterAsync("Pia");
        for (var i = 1; i <= 3; i++)
            await CreateTaskAsync(creator, $"Test: read api {i}", me.Id);

        Assert.Equal(3, await UnreadCountAsync(me));
        var mine = await NotificationsAsync(me);
        Assert.Equal(new[] { "Test: read api 3", "Test: read api 2", "Test: read api 1" },
            mine.Select(n => n.Message.Split('"')[1]));   // newest first

        // Paging
        var page2 = await ReadAsync<Page>(await me.Client.GetAsync("/api/notifications?page=2&pageSize=2"));
        Assert.Equal(3, page2.TotalCount);
        Assert.Single(page2.Items);

        // Mark one read (twice: idempotent)
        for (var i = 0; i < 2; i++)
        {
            var marked = await ReadAsync<NotificationBody>(await me.Client.PatchAsync($"/api/notifications/{mine[0].Id}/read", null));
            Assert.True(marked.IsRead);
        }
        Assert.Equal(2, await UnreadCountAsync(me));
        Assert.Equal(2, (await NotificationsAsync(me, "?unread=true")).Count);
        Assert.Equal(mine[0].Id, Assert.Single(await NotificationsAsync(me, "?unread=false")).Id);

        // Someone else's notification looks like it doesn't exist; they can't see or mark it.
        await (await someoneElse.Client.PatchAsync($"/api/notifications/{mine[1].Id}/read", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Notification not found");
        Assert.Empty(await NotificationsAsync(someoneElse));
        await (await me.Client.PatchAsync($"/api/notifications/{Guid.NewGuid()}/read", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound);

        // Read all
        Assert.Equal(2, await ReadAsync<int>(await me.Client.PostAsync("/api/notifications/read-all", null)));
        Assert.Equal(0, await UnreadCountAsync(me));
        Assert.Equal(0, await ReadAsync<int>(await me.Client.PostAsync("/api/notifications/read-all", null)));
        Assert.Equal(0, await UnreadCountAsync(someoneElse));   // untouched (has none)
    }

    [Fact]
    public async Task Notifications_need_a_token_and_bad_paging_is_400()
    {
        await (await factory.CreateAnonymousClient().GetAsync("/api/notifications")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);

        var me = await factory.RegisterAsync("Quinn");
        var problem = await (await me.Client.GetAsync("/api/notifications?pageSize=0")).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("PageSize", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task Deleting_a_task_keeps_its_notifications_without_the_link()
    {
        var creator = await factory.RegisterManagerAsync("Rita");
        var assignee = await factory.RegisterAsync("Sven");
        var taskId = await CreateTaskAsync(creator, "Test: deleted later", assignee.Id);

        Assert.Equal(HttpStatusCode.OK, (await creator.Client.DeleteAsync($"/api/tasks/{taskId}")).StatusCode);

        var n = Assert.Single(await NotificationsAsync(assignee));
        Assert.Null(n.RelatedTaskId);
    }

    // ---- SignalR ----

    [Fact]
    public async Task Assignee_receives_the_notification_over_SignalR_and_the_actor_does_not()
    {
        var creator = await factory.RegisterManagerAsync("Tara");
        var assignee = await factory.RegisterAsync("Uma");

        var received = new TaskCompletionSource<NotificationBody>(TaskCreationOptions.RunContinuationsAsynchronously);
        var actorReceived = 0;
        await using var assigneeHub = await ConnectAsync(assignee.Token);
        await using var creatorHub = await ConnectAsync(creator.Token);
        assigneeHub.On<NotificationBody>("ReceiveNotification", n => received.TrySetResult(n));
        creatorHub.On<NotificationBody>("ReceiveNotification", _ => Interlocked.Increment(ref actorReceived));

        var taskId = await CreateTaskAsync(creator, "Test: pushed", assignee.Id);

        var pushed = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal("TaskAssigned", pushed.Type);
        Assert.Equal(taskId, pushed.RelatedTaskId);
        Assert.False(pushed.IsRead);
        // Same notification as the stored one.
        Assert.Equal((await NotificationsAsync(assignee))[0].Id, pushed.Id);

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.Equal(0, actorReceived);
    }

    [Fact]
    public async Task Hub_rejects_connections_without_a_token()
    {
        var connection = BuildConnection(token: null);
        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        await connection.DisposeAsync();
    }

    // ---- helpers ----

    private sealed record NotificationBody(Guid Id, string Message, string Type, bool IsRead, Guid? RelatedTaskId, DateTime CreatedAt);

    private sealed record Page(List<NotificationBody> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);

    private sealed record IdOnly(Guid Id);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }

    private static async Task<Guid> CreateTaskAsync(TestUser creator, string title, Guid? assignedToId) =>
        (await ReadAsync<IdOnly>(await creator.Client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", assignedToId }))).Id;

    private static async Task CommentAsync(TestUser author, Guid taskId, string content) =>
        await ReadAsync<IdOnly>(await author.Client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content }));

    private static async Task PatchStatusAsync(TestUser user, Guid taskId, string status) =>
        await ReadAsync<IdOnly>(await user.Client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new { status }));

    private static async Task<List<NotificationBody>> NotificationsAsync(TestUser user, string query = "") =>
        (await ReadAsync<Page>(await user.Client.GetAsync($"/api/notifications{query}"))).Items;

    private static async Task<int> UnreadCountAsync(TestUser user) =>
        await ReadAsync<int>(await user.Client.GetAsync("/api/notifications/unread-count"));

    private HubConnection BuildConnection(string? token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                // In-memory TestServer: long polling over its HttpMessageHandler (no real sockets).
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                if (token is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = BuildConnection(token);
        await connection.StartAsync();
        return connection;
    }
}
