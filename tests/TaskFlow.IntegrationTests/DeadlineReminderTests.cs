using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Reminders;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Deadline reminders. The background job is off in the test API (Reminders:Enabled=false); the tests run
/// <see cref="DeadlineReminderService"/> directly or through POST /api/reminders/run. Each test registers its own
/// assignees, so it only looks at its own tasks (seed tasks may be reminded too).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DeadlineReminderTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Task_due_soon_gets_one_reminder_notification_and_email()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var assignee = await factory.RegisterAsync("Remy");
        var due = DateTime.UtcNow.AddHours(3);
        var taskId = await CreateTaskAsync(admin, "Test: due soon", assignee.Id, due);

        Assert.Contains(taskId, await RunRemindersAsync());

        var reminder = Assert.Single(await RemindersAsync(assignee));
        Assert.Equal(taskId, reminder.RelatedTaskId);
        Assert.Equal("\"Test: due soon\" is due in 2 hours", reminder.Message);
        Assert.False(reminder.IsRead);

        var email = Assert.Single(ReminderEmails(assignee));
        Assert.Equal($"Reminder: \"Test: due soon\" is due {due.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC",
            email.Subject);
        Assert.Equal("Remy Tester", email.ToName);
        Assert.Contains("\"Test: due soon\" is due in 2 hours", email.TextBody);

        // Idempotent: running again creates nothing new.
        Assert.DoesNotContain(taskId, await RunRemindersAsync());
        Assert.Single(await RemindersAsync(assignee));
        Assert.Single(ReminderEmails(assignee));
    }

    [Fact]
    public async Task Done_unassigned_far_off_overdue_and_inactive_assignee_tasks_get_no_reminder()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var assignee = await factory.RegisterAsync("Nora");
        var inactive = await factory.RegisterAsync("Ines");
        var soon = DateTime.UtcNow.AddHours(3);

        var done = await CreateTaskAsync(admin, "Test: already done", assignee.Id, soon);
        await ReadAsync<IdOnly>(await admin.PatchAsJsonAsync($"/api/tasks/{done}/status", new { status = "Done" }));
        var unassigned = await CreateTaskAsync(admin, "Test: unassigned", null, soon);
        var farOff = await CreateTaskAsync(admin, "Test: due in 3 days", assignee.Id, DateTime.UtcNow.AddDays(3));
        var overdue = await CreateTaskAsync(admin, "Test: overdue", assignee.Id, DateTime.UtcNow.AddHours(-1));
        var inactiveTask = await CreateTaskAsync(admin, "Test: inactive assignee", inactive.Id, soon);
        Assert.Equal(HttpStatusCode.OK,
            (await admin.PatchAsJsonAsync($"/api/users/{inactive.Id}/active", new { isActive = false })).StatusCode);

        var reminded = await RunRemindersAsync();

        Assert.DoesNotContain(done, reminded);
        Assert.DoesNotContain(unassigned, reminded);
        Assert.DoesNotContain(farOff, reminded);
        Assert.DoesNotContain(overdue, reminded);
        Assert.DoesNotContain(inactiveTask, reminded);
        Assert.Empty(await RemindersAsync(assignee));
        Assert.Empty(ReminderEmails(assignee));
        Assert.Empty(ReminderEmails(inactive));
    }

    [Fact]
    public async Task A_new_due_date_or_a_new_assignee_gets_a_new_reminder()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var first = await factory.RegisterAsync("Fern");
        var second = await factory.RegisterAsync("Gus");
        var taskId = await CreateTaskAsync(admin, "Test: rescheduled", first.Id, DateTime.UtcNow.AddHours(3));
        Assert.Contains(taskId, await RunRemindersAsync());

        // As if that reminder was sent 22 hours ago for an earlier due date and the task was rescheduled since: it is
        // older than "current due date (in 3 h) - 24 h", so it doesn't count for the current due date.
        await BackdateRemindersAsync(taskId, hours: 22);
        Assert.Contains(taskId, await RunRemindersAsync());
        Assert.Equal(2, (await RemindersAsync(first)).Count);

        // Reassigned: the new assignee gets their own reminder, once.
        await ReadAsync<IdOnly>(await admin.PatchAsJsonAsync($"/api/tasks/{taskId}/assignee", new { assignedToId = second.Id }));
        Assert.Contains(taskId, await RunRemindersAsync());
        Assert.DoesNotContain(taskId, await RunRemindersAsync());
        Assert.Single(await RemindersAsync(second));
        Assert.Equal(2, (await RemindersAsync(first)).Count);
    }

    // ---- POST /api/reminders/run ----

    [Fact]
    public async Task Run_endpoint_returns_the_reminded_tasks()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var assignee = await factory.RegisterAsync("Rita");
        var taskId = await CreateTaskAsync(admin, "Test: run endpoint", assignee.Id, DateTime.UtcNow.AddHours(5));

        var result = await ReadAsync<RunResult>(await admin.PostAsync("/api/reminders/run", null));
        Assert.Contains(taskId, result.TaskIds);
        Assert.Equal(result.TaskIds.Count, result.RemindersCreated);
        Assert.Equal("\"Test: run endpoint\" is due in 4 hours", Assert.Single(await RemindersAsync(assignee)).Message);

        var again = await ReadAsync<RunResult>(await admin.PostAsync("/api/reminders/run", null));
        Assert.DoesNotContain(taskId, again.TaskIds);
    }

    [Fact]
    public async Task Run_endpoint_is_admin_only()
    {
        var manager = await factory.LoginAsync(SeedUsers.Manager);
        await (await manager.PostAsync("/api/reminders/run", null)).ShouldBeProblemAsync(HttpStatusCode.Forbidden);

        var user = await factory.RegisterAsync("Uri");
        await (await user.Client.PostAsync("/api/reminders/run", null)).ShouldBeProblemAsync(HttpStatusCode.Forbidden);

        var anonymous = factory.CreateAnonymousClient();
        await (await anonymous.PostAsync("/api/reminders/run", null)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized);
    }

    // ---- helpers ----

    private sealed record NotificationBody(Guid Id, string Message, string Type, bool IsRead, Guid? RelatedTaskId, DateTime CreatedAt);

    private sealed record Page(List<NotificationBody> Items, int TotalCount);

    private sealed record IdOnly(Guid Id);

    private sealed record RunResult(int RemindersCreated, List<Guid> TaskIds);

    private async Task<IReadOnlyList<Guid>> RunRemindersAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DeadlineReminderService>().SendDueRemindersAsync();
    }

    private static async Task<List<NotificationBody>> RemindersAsync(TestUser user) =>
        (await ReadAsync<Page>(await user.Client.GetAsync("/api/notifications?pageSize=100"))).Items
        .Where(n => n.Type == "DeadlineApproaching").ToList();

    private List<Application.Common.Email.EmailMessage> ReminderEmails(TestUser user) =>
        factory.Emails.SentTo(user.Email).Where(m => m.Subject.StartsWith("Reminder:", StringComparison.Ordinal)).ToList();

    private async Task BackdateRemindersAsync(Guid taskId, int hours)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Notifications
            .Where(n => n.RelatedTaskId == taskId && n.Type == NotificationType.DeadlineApproaching)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.CreatedAt, n => n.CreatedAt.AddHours(-hours)));
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<T>>())!.Data!;
    }

    private static async Task<Guid> CreateTaskAsync(HttpClient creator, string title, Guid? assignedToId, DateTime due) =>
        (await ReadAsync<IdOnly>(await creator.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", dueDate = due, assignedToId }))).Id;
}
