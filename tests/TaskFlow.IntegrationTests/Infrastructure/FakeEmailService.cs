using System.Collections.Concurrent;
using TaskFlow.Application.Common.Email;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Infrastructure.Services.Email;

namespace TaskFlow.IntegrationTests.Infrastructure;

/// <summary>Replaces the SMTP queue and sender in the test API: records every email instead of sending it. Shared by all tests,
/// so look emails up by recipient (registered test users have unique addresses). Supports per-recipient failure simulation.</summary>
public sealed class FakeEmailService : IEmailService, IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();
    private readonly ConcurrentDictionary<string, string> _failRecipients = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEnabled => true;

    public void FailEmailsTo(string email, string errorMessage = "Simulated SMTP outage") =>
        _failRecipients[email] = errorMessage;

    public void ClearFailureFor(string email) =>
        _failRecipients.TryRemove(email, out _);

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_failRecipients.TryGetValue(message.ToEmail, out var error))
            throw new InvalidOperationException(error);

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The emails sent to <paramref name="email"/>, oldest first.</summary>
    public IReadOnlyList<EmailMessage> SentTo(string email) =>
        _sent.Where(m => string.Equals(m.ToEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();
}
