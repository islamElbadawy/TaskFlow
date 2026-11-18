using System.Globalization;
using System.Net;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Common.Email;

/// <summary>
/// Which notifications are also sent by email, and what those emails say. Only <see cref="NotificationType.TaskAssigned"/>
/// and <see cref="NotificationType.DeadlineApproaching"/> are emailed; the other types stay in-app only.
/// </summary>
public static class NotificationEmails
{
    public static bool IsEmailed(NotificationType type) =>
        type is NotificationType.TaskAssigned or NotificationType.DeadlineApproaching;

    /// <param name="notificationMessage">The notification's own text, e.g. <c>Manager User assigned you "Write docs"</c>.</param>
    public static EmailMessage Create(NotificationType type, string toEmail, string toName, string notificationMessage,
        string? taskTitle, DateTime? dueDate)
    {
        var title = taskTitle ?? "a task";
        var subject = type switch
        {
            NotificationType.DeadlineApproaching => $"Reminder: \"{title}\" is due {FormatDue(dueDate)}",
            NotificationType.TaskAssigned => $"New task: {title}",
            _ => notificationMessage
        };

        var due = dueDate is null ? "no due date" : FormatDue(dueDate);
        var text =
            $"Hi {toName},\r\n\r\n" +
            $"{notificationMessage}.\r\n\r\n" +
            $"Task: {title}\r\n" +
            $"Due: {due}\r\n\r\n" +
            "Open TaskFlow to see the task.\r\n";

        var html =
            $"<p>Hi {Encode(toName)},</p>" +
            $"<p>{Encode(notificationMessage)}.</p>" +
            "<table cellpadding=\"4\">" +
            $"<tr><td><strong>Task</strong></td><td>{Encode(title)}</td></tr>" +
            $"<tr><td><strong>Due</strong></td><td>{Encode(due)}</td></tr>" +
            "</table>" +
            "<p>Open TaskFlow to see the task.</p>";

        return new EmailMessage(toEmail, toName, subject, text, html);
    }

    private static string FormatDue(DateTime? dueDate) =>
        dueDate is { } d ? d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC" : "soon";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
