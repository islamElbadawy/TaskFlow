using TaskFlow.Application.Common.Email;

namespace TaskFlow.Application.Common.Interfaces;

/// <summary>
/// Sends email. Email is a side channel: <see cref="SendAsync"/> never throws for delivery problems (they are logged),
/// so it can't fail the operation that triggered it.
/// </summary>
public interface IEmailService
{
    /// <summary>False when email is switched off (<c>Smtp:Enabled</c>), so callers can skip building messages.</summary>
    bool IsEnabled { get; }

    /// <summary>Hands the message over for delivery (the SMTP implementation queues it and returns at once).</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
