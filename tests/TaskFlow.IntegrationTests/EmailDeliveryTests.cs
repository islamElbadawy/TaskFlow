using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Security;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Common.Email;
using TaskFlow.Domain.Enums;
using TaskFlow.Infrastructure.Services.Email;

namespace TaskFlow.IntegrationTests;

/// <summary>Real SMTP delivery to a throw-away Mailpit container. Independent of the API, so not in the API collection.</summary>
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

    private sealed record MailpitList(List<MailpitSummary> Messages);

    private sealed record MailpitSummary(string ID, string Subject, MailpitAddress From, List<MailpitAddress> To);

    private sealed record MailpitAddress(string Name, string Address);

    private sealed record MailpitMessage(string Text, string HTML);
}
