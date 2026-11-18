using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using TaskFlow.Application.Common.Email;

namespace TaskFlow.Infrastructure.Services.Email;

/// <summary>Delivers one message over SMTP with MailKit. Throws on failure; <see cref="EmailDispatcher"/> logs it.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>One SMTP connection per message: fine for the low volume of task emails.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly IOptions<SmtpSettings> _options;

    public SmtpEmailSender(IOptions<SmtpSettings> options)
    {
        _options = options;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = _options.Value;

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = settings.TimeoutSeconds * 1000 };
        await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty, cancellationToken);
        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
