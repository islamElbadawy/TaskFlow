using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Data.Seeding;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Board order: PATCH /api/tasks/{id}/move, where new and re-statused tasks land, GET /api/tasks?sort=position and the
/// AddTaskPosition backfill. Columns are shared with other tests, so assertions compare the positions of the tasks a
/// test creates rather than whole columns.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TaskPositionTests(TaskFlowApiFactory factory)
{
    // ---- move ----

    [Fact]
    public async Task Reorder_within_a_column_puts_the_task_above_its_new_neighbour()
    {
        var owner = await factory.RegisterAsync("Ada");
        var a = await CreateTaskAsync(owner, "Test: reorder a");
        var b = await CreateTaskAsync(owner, "Test: reorder b");
        var c = await CreateTaskAsync(owner, "Test: reorder c");
        var oldPositionOfA = (await GetTaskAsync(owner, a)).Position;

        var moved = await ReadTaskAsync(await MoveAsync(owner, c, "Todo", a));

        Assert.Equal("Todo", moved.Status);
        Assert.Equal(oldPositionOfA, moved.Position);
        Assert.Equal([c, a, b], await InPositionOrderAsync(owner, a, b, c));
        Assert.Equal(moved.Position + 1, (await GetTaskAsync(owner, a)).Position);
        Assert.Equal(moved.Position + 2, (await GetTaskAsync(owner, b)).Position);
        await AssertColumnIsNumberedFromZeroAsync(TaskItemStatus.Todo);
    }

    [Fact]
    public async Task Move_to_another_column_above_a_neighbour_and_both_columns_are_renumbered()
    {
        var owner = await factory.RegisterAsync("Bert");
        var x = await CreateTaskAsync(owner, "Test: across x");
        var y = await CreateTaskAsync(owner, "Test: across y");
        await ReadTaskAsync(await MoveAsync(owner, y, "InProgress", null));

        var moved = await ReadTaskAsync(await MoveAsync(owner, x, "InProgress", y));

        Assert.Equal("InProgress", moved.Status);
        Assert.Equal(moved.Position + 1, (await GetTaskAsync(owner, y)).Position);
        await AssertColumnIsNumberedFromZeroAsync(TaskItemStatus.InProgress);
        await AssertColumnIsNumberedFromZeroAsync(TaskItemStatus.Todo);   // the hole x left is closed
    }

    [Fact]
    public async Task Move_with_a_null_beforeTaskId_puts_the_task_at_the_end_of_the_column()
    {
        var owner = await factory.RegisterAsync("Cleo");
        var first = await CreateTaskAsync(owner, "Test: to the end first");
        var second = await CreateTaskAsync(owner, "Test: to the end second");

        var moved = await ReadTaskAsync(await MoveAsync(owner, first, "Todo", null));

        Assert.Equal([second, first], await InPositionOrderAsync(owner, first, second));
        Assert.Equal(await MaxPositionAsync(TaskItemStatus.Todo), moved.Position);
    }

    [Fact]
    public async Task beforeTaskId_equal_to_the_task_itself_is_400()
    {
        var owner = await factory.RegisterAsync("Dina");
        var task = await CreateTaskAsync(owner, "Test: before itself");

        var problem = await (await MoveAsync(owner, task, "Todo", task)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");

        Assert.Contains("A task can't be placed before itself", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task beforeTaskId_in_another_column_or_unknown_is_400_and_nothing_moves()
    {
        var owner = await factory.RegisterAsync("Emil");
        var task = await CreateTaskAsync(owner, "Test: wrong neighbour");
        var before = await GetTaskAsync(owner, task);

        await (await MoveAsync(owner, task, "Done", DataSeeder.TaskIds.WriteDocs))   // WriteDocs is InProgress
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "beforeTaskId must be a task in the Done column");
        await (await MoveAsync(owner, task, "Todo", Guid.NewGuid()))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "beforeTaskId must be a task in the Todo column");

        Assert.Equal(before, await GetTaskAsync(owner, task));
    }

    [Fact]
    public async Task Unknown_status_is_400_and_unknown_task_is_404()
    {
        var owner = await factory.RegisterAsync("Fynn");
        var task = await CreateTaskAsync(owner, "Test: bad status");

        var problem = await (await MoveAsync(owner, task, "Blocked", null)).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
        Assert.Contains("Status must be one of: Todo, InProgress, Done", problem.Extensions["errors"]!.ToString());

        await (await MoveAsync(owner, Guid.NewGuid(), "Todo", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
    }

    [Fact]
    public async Task Only_creator_assignee_manager_or_admin_may_move_a_task()
    {
        var creator = await factory.RegisterAsync("Gus");
        var assignee = await factory.RegisterAsync("Hedy");
        var outsider = await factory.RegisterAsync("Igor");
        var task = await CreateTaskAsync(creator, "Test: who may move", assignee.Id);

        await (await MoveAsync(outsider, task, "InProgress", null)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
        await (await MoveAsync(outsider, task, "Todo", null)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");   // reorder too
        Assert.Equal("Todo", (await GetTaskAsync(creator, task)).Status);

        Assert.Equal("InProgress", (await ReadTaskAsync(await MoveAsync(assignee, task, "InProgress", null))).Status);
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync($"/api/tasks/{task}/move",
            new { status = "Todo", beforeTaskId = (Guid?)null })).StatusCode);
    }

    // ---- notifications ----

    [Fact]
    public async Task A_status_change_through_move_notifies_like_PATCH_status_and_a_reorder_notifies_nobody()
    {
        var creator = await factory.RegisterAsync("Jana");
        var assignee = await factory.RegisterAsync("Kurt");
        var viaStatus = await CreateTaskAsync(creator, "Test: via status", assignee.Id);
        var viaMove = await CreateTaskAsync(creator, "Test: via move", assignee.Id);
        var assigned = (await NotificationsAsync(assignee)).Count;   // the two TaskAssigned

        // The assignee completes both: the creator gets one TaskCompleted each, the actor nothing.
        await ReadTaskAsync(await assignee.Client.PatchAsJsonAsync($"/api/tasks/{viaStatus}/status", new { status = "Done" }));
        await ReadTaskAsync(await MoveAsync(assignee, viaMove, "Done", null));

        var creators = await NotificationsAsync(creator);
        Assert.Equal(["TaskCompleted", "TaskCompleted"], creators.Select(n => n.Type));
        Assert.Equal("Kurt Tester marked \"Test: via move\" as done", creators[0].Message);
        Assert.Equal("Kurt Tester marked \"Test: via status\" as done", creators[1].Message);
        Assert.Equal(assigned, (await NotificationsAsync(assignee)).Count);

        // The creator reopens both: the assignee is told about each status change, with the same message.
        await ReadTaskAsync(await creator.Client.PatchAsJsonAsync($"/api/tasks/{viaStatus}/status", new { status = "InProgress" }));
        await ReadTaskAsync(await MoveAsync(creator, viaMove, "InProgress", null));

        var assignees = await NotificationsAsync(assignee);
        Assert.Equal(assigned + 2, assignees.Count);
        Assert.Equal(("TaskUpdated", "Jana Tester moved \"Test: via move\" to InProgress"), (assignees[0].Type, assignees[0].Message));
        Assert.Equal(("TaskUpdated", "Jana Tester moved \"Test: via status\" to InProgress"), (assignees[1].Type, assignees[1].Message));

        // A reorder inside the column changes no status: nobody is notified.
        await ReadTaskAsync(await MoveAsync(creator, viaMove, "InProgress", viaStatus));
        Assert.Equal(assigned + 2, (await NotificationsAsync(assignee)).Count);
        Assert.Equal(2, (await NotificationsAsync(creator)).Count);
    }

    // ---- where other writes put a task ----

    [Fact]
    public async Task A_new_task_lands_at_the_end_of_Todo()
    {
        var owner = await factory.RegisterAsync("Lena");
        await CreateTaskAsync(owner, "Test: earlier");

        var created = await ReadTaskAsync(await owner.Client.PostAsJsonAsync("/api/tasks",
            new { title = "Test: newest", description = "", priority = "Medium" }));

        Assert.Equal("Todo", created.Status);
        Assert.Equal(await MaxPositionAsync(TaskItemStatus.Todo), created.Position);
        Assert.Equal(1, await CountAtPositionAsync(TaskItemStatus.Todo, created.Position));
    }

    [Fact]
    public async Task PATCH_status_and_PUT_put_the_task_at_the_end_of_its_new_column()
    {
        var owner = await factory.RegisterAsync("Milo");
        var task = await CreateTaskAsync(owner, "Test: status to the end");

        var patched = await ReadTaskAsync(await owner.Client.PatchAsJsonAsync($"/api/tasks/{task}/status", new { status = "InProgress" }));
        Assert.Equal(await MaxPositionAsync(TaskItemStatus.InProgress), patched.Position);
        Assert.Equal(1, await CountAtPositionAsync(TaskItemStatus.InProgress, patched.Position));

        // The same status again is not a move.
        var same = await ReadTaskAsync(await owner.Client.PatchAsJsonAsync($"/api/tasks/{task}/status", new { status = "InProgress" }));
        Assert.Equal(patched.Position, same.Position);

        var put = await ReadTaskAsync(await owner.Client.PutAsJsonAsync($"/api/tasks/{task}", new
        {
            title = "Test: status to the end", description = "", priority = "Medium", status = "Done",
            dueDate = (DateTime?)null, assignedToId = (Guid?)null
        }));
        Assert.Equal(await MaxPositionAsync(TaskItemStatus.Done), put.Position);
    }

    // ---- reading ----

    [Fact]
    public async Task sort_position_lists_by_status_then_position()
    {
        var owner = await factory.RegisterAsync("Nora");
        var todo = await CreateTaskAsync(owner, "Test: sorted todo");
        await ReadTaskAsync(await MoveAsync(owner, await CreateTaskAsync(owner, "Test: sorted done"), "Done", null));

        var page = await ReadAsync<TaskPage>(await owner.Client.GetAsync("/api/tasks?sort=position&pageSize=100"));

        var statusRank = new Dictionary<string, int> { ["Todo"] = 0, ["InProgress"] = 1, ["Done"] = 2 };
        var keys = page.Items.Select(t => (statusRank[t.Status], t.Position)).ToList();
        Assert.Equal(keys.Order().ToList(), keys);
        Assert.Equal("Todo", page.Items[0].Status);

        var todoColumn = await ReadAsync<TaskPage>(await owner.Client.GetAsync("/api/tasks?sort=Position&status=Todo&pageSize=100"));
        Assert.All(todoColumn.Items, t => Assert.Equal("Todo", t.Status));
        Assert.Equal(todoColumn.Items.Select(t => t.Position).Order(), todoColumn.Items.Select(t => t.Position));
        Assert.Contains(todoColumn.Items, t => t.Id == todo);
    }

    [Fact]
    public async Task An_unknown_sort_is_400_and_the_default_is_still_newest_first()
    {
        var client = await factory.LoginAsync(SeedUsers.User);

        var problem = await (await client.GetAsync("/api/tasks?sort=title")).ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
        Assert.Contains("Sort must be one of: createdAt, position", problem.Extensions["errors"]!.ToString());

        var byDefault = await ReadAsync<TaskPage>(await client.GetAsync("/api/tasks?pageSize=100"));
        var byCreatedAt = await ReadAsync<TaskPage>(await client.GetAsync("/api/tasks?sort=createdAt&pageSize=100"));
        Assert.Equal(byDefault.Items.Select(t => t.CreatedAt).OrderDescending(), byDefault.Items.Select(t => t.CreatedAt));
        Assert.Equal(byDefault.Items.Select(t => t.Id), byCreatedAt.Items.Select(t => t.Id));
    }

    // ---- migration ----

    [Fact]
    public async Task The_migration_backfills_each_column_by_due_date_then_creation_time()
    {
        // A second database in the same container: started in Development, the host migrates and seeds it. Rolling
        // back to the previous migration drops the column; migrating again re-adds it and runs the backfill.
        var connection = new NpgsqlConnectionStringBuilder(factory.ConnectionString)
        {
            Database = $"taskflow_backfill_{Guid.NewGuid():N}"
        };
        await using var host = factory.WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            Assert.Equal(connection.Database, db.Database.GetDbConnection().Database);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261118110156_AddUserSecurityStamp");
            await migrator.MigrateAsync();

            var tasks = await db.Tasks.AsNoTracking().Select(t => new { t.Id, t.Status, t.Position }).ToListAsync();
            Guid[] Column(TaskItemStatus status) =>
                tasks.Where(t => t.Status == status).OrderBy(t => t.Position).Select(t => t.Id).ToArray();

            Assert.All(Enum.GetValues<TaskItemStatus>(), status => Assert.Equal(
                Enumerable.Range(0, Column(status).Length), tasks.Where(t => t.Status == status).Select(t => t.Position).Order()));
            // Due date first (no due date last), then creation time; a plain creation-time order would differ.
            Assert.Equal([DataSeeder.TaskIds.FixPasswordReset, DataSeeder.TaskIds.SetupCi, DataSeeder.TaskIds.DesignNotifications],
                Column(TaskItemStatus.Todo));
            Assert.Equal([DataSeeder.TaskIds.ReviewBacklog, DataSeeder.TaskIds.WriteDocs, DataSeeder.TaskIds.MigrateToPostgres],
                Column(TaskItemStatus.InProgress));
            Assert.Equal([DataSeeder.TaskIds.SetupProject, DataSeeder.TaskIds.ArchiveReports], Column(TaskItemStatus.Done));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    // ---- helpers ----

    private sealed record TaskBody(Guid Id, string Title, string Status, int Position, Guid? AssignedToId, DateTime? UpdatedAt);

    private sealed record TaskListItem(Guid Id, string Status, int Position, DateTime CreatedAt);

    private sealed record TaskPage(List<TaskListItem> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);

    private sealed record NotificationBody(string Type, string Message);

    private sealed record NotificationPage(List<NotificationBody> Items);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }

    private static Task<TaskBody> ReadTaskAsync(HttpResponseMessage response) => ReadAsync<TaskBody>(response);

    private static async Task<Guid> CreateTaskAsync(TestUser creator, string title, Guid? assignedToId = null) =>
        (await ReadTaskAsync(await creator.Client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", assignedToId }))).Id;

    private static Task<HttpResponseMessage> MoveAsync(TestUser user, Guid taskId, string status, Guid? beforeTaskId) =>
        user.Client.PatchAsJsonAsync($"/api/tasks/{taskId}/move", new { status, beforeTaskId });

    private static async Task<TaskBody> GetTaskAsync(TestUser user, Guid taskId) =>
        await ReadTaskAsync(await user.Client.GetAsync($"/api/tasks/{taskId}"));

    /// <summary>The given tasks (all in one column) sorted by their current position.</summary>
    private static async Task<Guid[]> InPositionOrderAsync(TestUser user, params Guid[] taskIds)
    {
        var tasks = new List<TaskBody>();
        foreach (var id in taskIds)
            tasks.Add(await GetTaskAsync(user, id));
        return tasks.OrderBy(t => t.Position).Select(t => t.Id).ToArray();
    }

    private static async Task<List<NotificationBody>> NotificationsAsync(TestUser user) =>
        (await ReadAsync<NotificationPage>(await user.Client.GetAsync("/api/notifications"))).Items;

    private async Task<int> MaxPositionAsync(TaskItemStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Tasks.Where(t => t.Status == status).MaxAsync(t => t.Position);
    }

    private async Task<int> CountAtPositionAsync(TaskItemStatus status, int position)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Tasks.CountAsync(t => t.Status == status && t.Position == position);
    }

    /// <summary>After a move the column holds exactly the positions 0..n-1.</summary>
    private async Task AssertColumnIsNumberedFromZeroAsync(TaskItemStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var positions = await db.Tasks.Where(t => t.Status == status).OrderBy(t => t.Position).Select(t => t.Position).ToListAsync();
        Assert.Equal(Enumerable.Range(0, positions.Count), positions);
    }
}
