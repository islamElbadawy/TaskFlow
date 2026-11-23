using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.Infrastructure.Data.Seeding;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>PUT/PATCH/DELETE on tasks. Every test works on tasks it creates, so the seed data stays unchanged.</summary>
[Collection(ApiCollection.Name)]
public sealed class TaskLifecycleTests(TaskFlowApiFactory factory)
{
    private static readonly Guid ManagerId = DataSeeder.UserIds.Manager;
    private static readonly Guid UserId = DataSeeder.UserIds.User;
    private static readonly Guid User2Id = DataSeeder.UserIds.User2;
    private static readonly Guid InactiveId = DataSeeder.UserIds.Inactive;

    // ---- PUT ----

    [Fact]
    public async Task Creator_can_update_every_field_and_GET_shows_it()
    {
        var creator = await factory.RegisterManagerAsync("Creator");
        var task = await CreateTaskAsync(creator.Client, "Test: creator updates");
        var due = new DateTime(2030, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        var response = await creator.Client.PutAsJsonAsync($"/api/tasks/{task.Id}", new
        {
            title = "Test: creator updated",
            description = "new description",
            priority = "high",
            status = "InProgress",
            dueDate = due,
            assignedToId = User2Id
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadTaskAsync(response);
        Assert.Equal("Test: creator updated", updated.Title);
        Assert.Equal("new description", updated.Description);
        Assert.Equal("High", updated.Priority);
        Assert.Equal("InProgress", updated.Status);
        Assert.Equal(due, updated.DueDate);
        Assert.Equal(User2Id, updated.AssignedToId);
        Assert.Equal("Sam Taylor", updated.AssignedToName);
        Assert.NotNull(updated.UpdatedAt);

        var reread = await ReadTaskAsync(await creator.Client.GetAsync($"/api/tasks/{task.Id}"));
        Assert.Equal(updated, reread);
    }

    [Fact]
    public async Task Manager_and_admin_can_update_a_task_they_did_not_create()
    {
        var creator = await factory.RegisterManagerAsync("Origin");
        var task = await CreateTaskAsync(creator.Client, "Test: updated by others");

        foreach (var email in new[] { SeedUsers.Manager, SeedUsers.Admin })
        {
            var client = await factory.LoginAsync(email);
            var response = await client.PutAsJsonAsync($"/api/tasks/{task.Id}", UpdateBody($"Test: updated by {email}"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Another_user_cannot_update_change_status_assign_or_delete_403()
    {
        var task = await CreateTaskAsync(await factory.LoginAsync(SeedUsers.Manager), "Test: not yours", assignedToId: UserId);
        var outsider = await factory.LoginAsync(SeedUsers.User2);

        await (await outsider.PutAsJsonAsync($"/api/tasks/{task.Id}", UpdateBody("Test: hijacked")))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
        await (await outsider.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { status = "Done" }))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
        await (await outsider.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new { assignedToId = User2Id }))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
        await (await outsider.DeleteAsync($"/api/tasks/{task.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");

        var unchanged = await ReadTaskAsync(await outsider.GetAsync($"/api/tasks/{task.Id}"));
        Assert.Equal("Todo", unchanged.Status);
        Assert.Equal(UserId, unchanged.AssignedToId);
    }

    [Fact]
    public async Task Update_with_unknown_status_is_400_validation()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var task = await CreateTaskAsync(manager, "Test: bad status");

        var response = await manager.PutAsJsonAsync($"/api/tasks/{task.Id}", UpdateBody("Test: bad status") with { status = "Finished" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Status", problem.Extensions["errors"]!.ToString());
    }

    [Fact]
    public async Task Update_to_an_inactive_assignee_is_400_but_keeping_one_is_allowed()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var task = await CreateTaskAsync(manager, "Test: inactive assignee");

        await (await manager.PutAsJsonAsync($"/api/tasks/{task.Id}", UpdateBody("Test: x") with { assignedToId = InactiveId }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "Assigned user is inactive");

        // A task that is already assigned to a since-deactivated user can still be edited without reassigning it.
        var legacyTaskId = await InsertTaskAsync("Test: legacy inactive assignee", ManagerId, InactiveId);
        var response = await manager.PutAsJsonAsync($"/api/tasks/{legacyTaskId}",
            UpdateBody("Test: legacy, edited") with { assignedToId = InactiveId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_task_is_404_for_every_write()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var id = Guid.NewGuid();

        await (await admin.PutAsJsonAsync($"/api/tasks/{id}", UpdateBody("Test: ghost")))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
        await (await admin.PatchAsJsonAsync($"/api/tasks/{id}/status", new { status = "Done" }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
        await (await admin.PatchAsJsonAsync($"/api/tasks/{id}/assignee", new { assignedToId = (Guid?)null }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
        await (await admin.DeleteAsync($"/api/tasks/{id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound", "Task not found");
    }

    // ---- PATCH status ----

    [Fact]
    public async Task Assignee_can_change_status_but_not_edit_or_reassign()
    {
        var task = await CreateTaskAsync(await factory.LoginAsync(SeedUsers.Manager), "Test: assignee status", assignedToId: User2Id);
        var assignee = await factory.LoginAsync(SeedUsers.User2);

        var response = await assignee.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { status = "done" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Done", (await ReadTaskAsync(response)).Status);

        await (await assignee.PutAsJsonAsync($"/api/tasks/{task.Id}", UpdateBody("Test: assignee edit")))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
        await (await assignee.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new { assignedToId = UserId }))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden");
    }

    [Fact]
    public async Task Change_status_with_unknown_name_is_400()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var task = await CreateTaskAsync(manager, "Test: bad patch status");

        var response = await manager.PatchAsJsonAsync($"/api/tasks/{task.Id}/status", new { status = "Archived" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        Assert.Contains("Status", problem.Extensions["errors"]!.ToString());
    }

    // ---- PATCH assignee ----

    [Fact]
    public async Task Assign_and_unassign()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var task = await CreateTaskAsync(manager, "Test: assign");

        var assigned = await ReadTaskAsync(await manager.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new { assignedToId = UserId }));
        Assert.Equal(UserId, assigned.AssignedToId);
        Assert.Equal("Regular User", assigned.AssignedToName);

        var unassigned = await ReadTaskAsync(await manager.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new { assignedToId = (Guid?)null }));
        Assert.Null(unassigned.AssignedToId);
        Assert.Null(unassigned.AssignedToName);
    }

    [Fact]
    public async Task Assigning_an_inactive_or_unknown_user_is_400()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var task = await CreateTaskAsync(manager, "Test: bad assignee");

        await (await manager.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new { assignedToId = InactiveId }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "Assigned user is inactive");
        await (await manager.PatchAsJsonAsync($"/api/tasks/{task.Id}/assignee", new { assignedToId = Guid.NewGuid() }))
            .ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation", "Assigned user not found");
    }

    // ---- DELETE ----

    [Fact]
    public async Task Only_creator_or_admin_can_delete()
    {
        var creator = await factory.RegisterManagerAsync("Deleter");
        var mine = await CreateTaskAsync(creator.Client, "Test: delete own");
        var forAdmin = await CreateTaskAsync(creator.Client, "Test: deleted by admin");

        // Another Manager may edit anyone's task but not delete it.
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        await (await manager.DeleteAsync($"/api/tasks/{mine.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Forbidden", "Only the task's creator or an Admin can delete this task.");

        Assert.Equal(HttpStatusCode.OK, (await creator.Client.DeleteAsync($"/api/tasks/{mine.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.LoginAsync(SeedUsers.Admin)).DeleteAsync($"/api/tasks/{forAdmin.Id}")).StatusCode);

        await (await creator.Client.GetAsync($"/api/tasks/{mine.Id}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);
        await (await creator.Client.GetAsync($"/api/tasks/{forAdmin.Id}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_comments_and_keeps_notifications_without_the_link()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        var task = await CreateTaskAsync(manager, "Test: delete with children", assignedToId: UserId);

        Guid commentId, notificationId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var comment = new Comment { TaskId = task.Id, UserId = UserId, Content = "Test comment" };
            var notification = new Notification
            {
                UserId = UserId, RelatedTaskId = task.Id, Type = NotificationType.TaskAssigned, Message = "Test notification"
            };
            db.AddRange(comment, notification);
            await db.SaveChangesAsync();
            (commentId, notificationId) = (comment.Id, notification.Id);
        }

        Assert.Equal(HttpStatusCode.OK, (await manager.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Set<Comment>().AnyAsync(c => c.Id == commentId));
            var notification = await db.Set<Notification>().AsNoTracking().SingleAsync(n => n.Id == notificationId);
            Assert.Null(notification.RelatedTaskId);
        }
    }

    // ---- helpers ----

    private sealed record TaskBody(
        Guid Id, string Title, string Description, string Status, string Priority, DateTime? DueDate,
        Guid CreatedById, string CreatedByName, Guid? AssignedToId, string? AssignedToName,
        DateTime CreatedAt, DateTime? UpdatedAt, int CommentsCount);

    private sealed record UpdateRequest(
        string title, string description, string priority, string status, DateTime? dueDate, Guid? assignedToId);

    private static UpdateRequest UpdateBody(string title) => new(title, "", "Medium", "Todo", null, null);

    private static async Task<TaskBody> CreateTaskAsync(HttpClient client, string title, Guid? assignedToId = null)
    {
        var response = await client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", assignedToId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadTaskAsync(response);
    }

    private static async Task<TaskBody> ReadTaskAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResult<TaskBody>>();
        return body!.Data!;
    }

    /// <summary>Inserts a task directly, e.g. one assigned to a user that the API would refuse as a new assignee.</summary>
    private async Task<Guid> InsertTaskAsync(string title, Guid createdBy, Guid? assignedTo)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var task = new TaskItem { Title = title, CreatedByUserId = createdBy, AssignedToUserId = assignedTo };
        db.Add(task);
        await db.SaveChangesAsync();
        return task.Id;
    }
}
