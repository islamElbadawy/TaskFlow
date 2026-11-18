using System.Collections.Concurrent;
using TaskFlow.Application.Common.Email;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.IntegrationTests.Infrastructure;

/// <summary>Replaces the SMTP queue in the test API: records every email instead of sending it. Shared by all tests,
/// so look emails up by recipient (registered test users have unique addresses).</summary>
public sealed class FakeEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public bool IsEnabled => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The emails sent to <paramref name="email"/>, oldest first.</summary>
    public IReadOnlyList<EmailMessage> SentTo(string email) =>
        _sent.Where(m => string.Equals(m.ToEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();
}
