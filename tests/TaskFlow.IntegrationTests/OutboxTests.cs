using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Data.Context;
using TaskFlow.IntegrationTests.Infrastructure;

namespace TaskFlow.IntegrationTests;

/// <summary>
/// Integration tests for the transactional outbox: atomic persistence, online vs. offline push flush on
/// <c>NotificationHub.OnConnectedAsync</c>, deactivated user skipping, SMTP failure retry/recovery with
/// exponential backoff, dead-lettering after MaxAttempts, and Sent row retention cleanup.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OutboxTests(TaskFlowApiFactory factory)
{
    [Fact]
    public async Task Assigning_a_task_atomically_stages_notification_and_push_plus_email_outbox_rows()
    {
        var creator = await factory.RegisterManagerAsync("OutboxCreator");
        var assignee = await factory.RegisterAsync("OutboxAssignee");

        var taskId = await CreateTaskAsync(creator, "Outbox: atomic write", assignee.Id);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var notification = await db.Notifications
            .AsNoTracking()
            .SingleAsync(n => n.UserId == assignee.Id && n.RelatedTaskId == taskId);

        var outboxRows = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.NotificationId == notification.Id)
            .ToListAsync();

        Assert.Equal(2, outboxRows.Count);

        var push = Assert.Single(outboxRows, m => m.Channel == OutboxChannel.Push);
        Assert.Equal(assignee.Id, push.RecipientUserId);
        Assert.Equal(OutboxStatus.Pending, push.Status); // assignee is offline
        Assert.Contains("Outbox: atomic write", push.PayloadJson);

        var email = Assert.Single(outboxRows, m => m.Channel == OutboxChannel.Email);
        Assert.Equal(assignee.Id, email.RecipientUserId);
        Assert.Equal(OutboxStatus.Sent, email.Status); // immediate fast-path succeeded via FakeEmailService
        Assert.NotNull(email.ProcessedAt);
        Assert.Contains(assignee.Email, email.PayloadJson);

        // Non-emailed notification (CommentAdded) creates Push outbox row only (no Email row).
        var commentResp = await creator.Client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { content = "Ping" });
        Assert.Equal(HttpStatusCode.OK, commentResp.StatusCode);

        var commentNotification = await db.Notifications
            .AsNoTracking()
            .SingleAsync(n => n.UserId == assignee.Id && n.Type == NotificationType.CommentAdded);

        var commentOutbox = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.NotificationId == commentNotification.Id)
            .ToListAsync();

        var commentPush = Assert.Single(commentOutbox);
        Assert.Equal(OutboxChannel.Push, commentPush.Channel);
        Assert.Equal(OutboxStatus.Pending, commentPush.Status);
    }

    [Fact]
    public async Task Online_user_receives_immediate_push_and_offline_user_flushes_on_session_open()
    {
        var creator = await factory.RegisterManagerAsync("PushCreator");
        var onlineUser = await factory.RegisterAsync("OnlineUser");
        var offlineUser = await factory.RegisterAsync("OfflineUser");

        // 1. Online user connects first -> receives immediate SignalR push and row is marked Sent.
        var onlineReceived = new TaskCompletionSource<NotificationBody>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var onlineHub = BuildConnection(onlineUser.Token))
        {
            onlineHub.On<NotificationBody>("ReceiveNotification", n => onlineReceived.TrySetResult(n));
            await onlineHub.StartAsync();

            var onlineTaskId = await CreateTaskAsync(creator, "Outbox: online push", onlineUser.Id);
            var pushed = await onlineReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal("TaskAssigned", pushed.Type);
            Assert.Equal(onlineTaskId, pushed.RelatedTaskId);

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pushRow = await db.OutboxMessages.AsNoTracking()
                .SingleAsync(m => m.RecipientUserId == onlineUser.Id && m.Channel == OutboxChannel.Push);
            Assert.Equal(OutboxStatus.Sent, pushRow.Status);
            Assert.NotNull(pushRow.ProcessedAt);
        }

        // 2. Offline user is NOT connected -> Push rows stay Pending until they open a new session.
        var offlineTaskId = await CreateTaskAsync(creator, "Outbox: offline push", offlineUser.Id);
        Assert.Equal(HttpStatusCode.OK,
            (await creator.Client.PostAsJsonAsync($"/api/tasks/{offlineTaskId}/comments", new { content = "While offline" })).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pendingRows = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.RecipientUserId == offlineUser.Id && m.Channel == OutboxChannel.Push)
                .ToListAsync();
            Assert.Equal(2, pendingRows.Count);
            Assert.All(pendingRows, r => Assert.Equal(OutboxStatus.Pending, r.Status));
        }

        // 3. Offline user opens a session -> OnConnectedAsync flushes both Pending push notifications.
        var flushed = new ConcurrentQueue<NotificationBody>();
        var allFlushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var offlineHub = BuildConnection(offlineUser.Token))
        {
            offlineHub.On<NotificationBody>("ReceiveNotification", n =>
            {
                flushed.Enqueue(n);
                if (flushed.Count >= 2)
                    allFlushed.TrySetResult();
            });
            await offlineHub.StartAsync();
            await allFlushed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        }

        Assert.Equal(2, flushed.Count);
        Assert.Contains(flushed, n => n.Type == "TaskAssigned" && n.RelatedTaskId == offlineTaskId);
        Assert.Contains(flushed, n => n.Type == "CommentAdded" && n.RelatedTaskId == offlineTaskId);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var rows = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.RecipientUserId == offlineUser.Id && m.Channel == OutboxChannel.Push)
                .ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r =>
            {
                Assert.Equal(OutboxStatus.Sent, r.Status);
                Assert.NotNull(r.ProcessedAt);
            });
        }
    }

    [Fact]
    public async Task FlushPendingPushForUserAsync_skips_deactivated_users()
    {
        var admin = await factory.LoginAsync(SeedUsers.Admin);
        var target = await factory.RegisterAsync("DeactivatedPushUser");

        await CreateTaskAsync(admin, "Outbox: deactivated user", target.Id);

        // Deactivate user while Push outbox row is still Pending.
        var deactivate = await admin.PatchAsJsonAsync($"/api/users/{target.Id}/active", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        using var scope = factory.Services.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
        var flushed = await processor.FlushPendingPushForUserAsync(target.Id);
        Assert.Equal(0, flushed);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var pushRow = await db.OutboxMessages.AsNoTracking()
            .SingleAsync(m => m.RecipientUserId == target.Id && m.Channel == OutboxChannel.Push);
        Assert.Equal(OutboxStatus.Pending, pushRow.Status);
    }

    [Fact]
    public async Task Failed_email_retries_with_backoff_and_recovers_when_smtp_returns()
    {
        var creator = await factory.RegisterManagerAsync("RetryCreator");
        var assignee = await factory.RegisterAsync("RetryAssignee");
        var admin = await factory.LoginAsync(SeedUsers.Admin);

        factory.Emails.FailEmailsTo(assignee.Email, "SMTP 421 temporary outage");
        try
        {
            // User request still succeeds even when SMTP is down.
            await CreateTaskAsync(creator, "Outbox: retry recovery", assignee.Id);

            OutboxMessage emailRow;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                emailRow = await db.OutboxMessages.AsNoTracking()
                    .SingleAsync(m => m.RecipientUserId == assignee.Id && m.Channel == OutboxChannel.Email);
            }

            Assert.Equal(OutboxStatus.Pending, emailRow.Status);
            Assert.Equal(1, emailRow.AttemptCount);
            Assert.Contains("SMTP 421 temporary outage", emailRow.LastError);
            Assert.True(emailRow.NextAttemptAt > DateTime.UtcNow.AddMinutes(5));
            Assert.Empty(factory.Emails.SentTo(assignee.Email));

            // Polling before NextAttemptAt does not retry early.
            using (var scope = factory.Services.CreateScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.ProcessDueEmailsAsync();
            }
            Assert.Empty(factory.Emails.SentTo(assignee.Email));

            // Restore SMTP and advance row to due now, then trigger via Admin endpoint POST /api/outbox/process.
            factory.Emails.ClearFailureFor(assignee.Email);
            await MakeDueNowAsync(emailRow.Id);

            var processResp = await admin.PostAsync("/api/outbox/process", null);
            Assert.Equal(HttpStatusCode.OK, processResp.StatusCode);

            var sent = Assert.Single(factory.Emails.SentTo(assignee.Email));
            Assert.Equal("New task: Outbox: retry recovery", sent.Subject);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updated = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == emailRow.Id);
                Assert.Equal(OutboxStatus.Sent, updated.Status);
                Assert.NotNull(updated.ProcessedAt);
                Assert.Null(updated.LastError);
            }
        }
        finally
        {
            factory.Emails.ClearFailureFor(assignee.Email);
        }
    }

    [Fact]
    public async Task Email_moves_to_dead_letter_after_exceeding_max_attempts()
    {
        var creator = await factory.RegisterManagerAsync("DeadLetterCreator");
        var assignee = await factory.RegisterAsync("DeadLetterAssignee");

        factory.Emails.FailEmailsTo(assignee.Email, "SMTP 550 mailbox unavailable");
        try
        {
            await CreateTaskAsync(creator, "Outbox: dead letter", assignee.Id);

            Guid emailRowId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.OutboxMessages.AsNoTracking()
                    .SingleAsync(m => m.RecipientUserId == assignee.Id && m.Channel == OutboxChannel.Email);
                emailRowId = row.Id;
                Assert.Equal(1, row.AttemptCount);
                Assert.Equal(OutboxStatus.Pending, row.Status);
            }

            // Attempts 2..5 (MaxAttempts = 5)
            for (var expectedAttempt = 2; expectedAttempt <= 5; expectedAttempt++)
            {
                await MakeDueNowAsync(emailRowId);
                using var scope = factory.Services.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.ProcessDueEmailsAsync();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == emailRowId);
                Assert.Equal(expectedAttempt, row.AttemptCount);
                Assert.Contains("SMTP 550 mailbox unavailable", row.LastError);

                var expectedStatus = expectedAttempt < 5 ? OutboxStatus.Pending : OutboxStatus.DeadLetter;
                Assert.Equal(expectedStatus, row.Status);
            }

            // Once DeadLetter, even if SMTP recovers and NextAttemptAt is due, it is never retried.
            factory.Emails.ClearFailureFor(assignee.Email);
            await MakeDueNowAsync(emailRowId);

            using (var scope = factory.Services.CreateScope())
            {
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                await processor.ProcessDueEmailsAsync();

                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == emailRowId);
                Assert.Equal(OutboxStatus.DeadLetter, row.Status);
                Assert.Equal(5, row.AttemptCount);
            }

            Assert.Empty(factory.Emails.SentTo(assignee.Email));
        }
        finally
        {
            factory.Emails.ClearFailureFor(assignee.Email);
        }
    }

    [Fact]
    public async Task CleanupSentMessagesAsync_purges_old_sent_rows_and_keeps_recent_ones()
    {
        var oldSentId = Guid.CreateVersion7();
        var recentSentId = Guid.CreateVersion7();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OutboxMessages.AddRange(
                new OutboxMessage
                {
                    Id = oldSentId,
                    Channel = OutboxChannel.Email,
                    Status = OutboxStatus.Sent,
                    PayloadJson = "{}",
                    NextAttemptAt = DateTime.UtcNow.AddDays(-10),
                    ProcessedAt = DateTime.UtcNow.AddDays(-10),
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                },
                new OutboxMessage
                {
                    Id = recentSentId,
                    Channel = OutboxChannel.Email,
                    Status = OutboxStatus.Sent,
                    PayloadJson = "{}",
                    NextAttemptAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();

            var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
            var deleted = await processor.CleanupSentMessagesAsync();
            Assert.True(deleted >= 1);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.OutboxMessages.AnyAsync(m => m.Id == oldSentId));
            Assert.True(await db.OutboxMessages.AnyAsync(m => m.Id == recentSentId));
        }
    }

    // ---- helpers ----

    private sealed record NotificationBody(Guid Id, string Message, string Type, bool IsRead, Guid? RelatedTaskId, DateTime CreatedAt);
    private sealed record IdOnly(Guid Id);

    private static async Task<Guid> CreateTaskAsync(TestUser creator, string title, Guid? assignedToId) =>
        await CreateTaskAsync(creator.Client, title, assignedToId);

    private static async Task<Guid> CreateTaskAsync(HttpClient client, string title, Guid? assignedToId)
    {
        var response = await client.PostAsJsonAsync("/api/tasks",
            new { title, description = "", priority = "Medium", dueDate = (DateTime?)null, assignedToId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ApiResult<IdOnly>>())!.Data!.Id;
    }

    private async Task MakeDueNowAsync(Guid outboxMessageId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.OutboxMessages
            .Where(m => m.Id == outboxMessageId)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1)));
    }

    private HubConnection BuildConnection(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/notifications"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
}
