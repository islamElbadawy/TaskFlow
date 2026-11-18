using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Email;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Services.Email;

namespace TaskFlow.IntegrationTests;

/// <summary>The email pipeline below <c>IEmailService</c>: the queue, the background dispatcher and real SMTP delivery
/// to a throw-away Mailpit container. Independent of the API, so not in the API collection.</summary>
public sealed class EmailDeliveryTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.33")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request.ForPort(8025).ForPath("/api/v1/messages")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public async Task DisposeAsync() => await _mailpit.DisposeAsync();

    [Fact]
    public async Task Smtp_sender_delivers_a_text_and_html_email()
    {
        var sender = new SmtpEmailSender(Options.Create(new SmtpSettings
        {
            Enabled = true,
            Host = _mailpit.Hostname,
            Port = _mailpit.GetMappedPublicPort(1025),
            Security = SecureSocketOptions.None,
            FromAddress = "noreply@taskflow.dev",
            FromName = "TaskFlow"
        }));
        var message = NotificationEmails.Create(NotificationType.DeadlineApproaching, "bob@taskflow.test", "Bob Tester",
            "\"Write docs\" is due in 5 hours", "Write docs", new DateTime(2026, 11, 20, 14, 30, 0, DateTimeKind.Utc));

        await sender.SendAsync(message, CancellationToken.None);

        using var mailpit = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        var list = await mailpit.GetFromJsonAsync<MailpitList>("/api/v1/messages");
        var summary = Assert.Single(list!.Messages);
        Assert.Equal("Reminder: \"Write docs\" is due 2026-11-20 14:30 UTC", summary.Subject);
        Assert.Equal("noreply@taskflow.dev", summary.From.Address);
        var to = Assert.Single(summary.To);
        Assert.Equal(("Bob Tester", "bob@taskflow.test"), (to.Name, to.Address));

        var full = await mailpit.GetFromJsonAsync<MailpitMessage>($"/api/v1/message/{summary.ID}");
        Assert.Contains("Hi Bob Tester,", full!.Text);
        Assert.Contains("Due: 2026-11-20 14:30 UTC", full.Text);
        Assert.Contains("<strong>Task</strong>", full.HTML);
    }

    [Fact]
    public void Disabled_queue_drops_messages()
    {
        var queue = new EmailQueue(Options.Create(new SmtpSettings { Enabled = false }), NullLogger<EmailQueue>.Instance);

        _ = queue.SendAsync(Message("dropped"));

        Assert.False(queue.IsEnabled);
        Assert.False(queue.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Dispatcher_logs_a_failed_email_and_keeps_sending_the_next_ones()
    {
        var queue = new EmailQueue(Options.Create(new SmtpSettings { Enabled = true }), NullLogger<EmailQueue>.Instance);
        var sender = new FlakySender(failSubject: "first");
        IHostedService dispatcher = new EmailDispatcher(queue, sender, NullLogger<EmailDispatcher>.Instance);

        await queue.SendAsync(Message("first"));
        await queue.SendAsync(Message("second"));
        await dispatcher.StartAsync(CancellationToken.None);
        try
        {
            var delivered = await sender.Delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("second", delivered.Subject);
        }
        finally
        {
            await dispatcher.StopAsync(CancellationToken.None);
        }
    }

    private static EmailMessage Message(string subject) => new("x@taskflow.test", "X", subject, "text", "<p>html</p>");

    private sealed class FlakySender(string failSubject) : IEmailSender
    {
        public TaskCompletionSource<EmailMessage> Delivered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (message.Subject == failSubject)
                throw new InvalidOperationException("SMTP server unavailable");
            Delivered.TrySetResult(message);
            return Task.CompletedTask;
        }
    }

    private sealed record MailpitList(List<MailpitSummary> Messages);

    private sealed record MailpitSummary(string ID, string Subject, MailpitAddress From, List<MailpitAddress> To);

    private sealed record MailpitAddress(string Name, string Address);

    private sealed record MailpitMessage(string Text, string HTML);
}
