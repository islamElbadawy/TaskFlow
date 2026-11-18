using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace TaskFlow.Infrastructure.Services.Email;

/// <summary>
/// The <c>Smtp</c> configuration section, validated on start. Email is off unless <see cref="Enabled"/> is true.
/// The defaults match the Mailpit service in docker-compose.yml (SMTP on localhost:1025, no TLS, no login).
/// Put a real server's <see cref="Username"/>/<see cref="Password"/> in user-secrets or environment variables.
/// </summary>
public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 1025;

    /// <summary><c>Auto</c> uses TLS on port 465 and STARTTLS when the server offers it; <c>None</c> for plain SMTP.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.Auto;

    /// <summary>Optional: no authentication when empty.</summary>
    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = "noreply@taskflow.dev";

    [Required]
    public string FromName { get; set; } = "TaskFlow";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
