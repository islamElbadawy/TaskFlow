namespace TaskFlow.Application.Common.Email;

/// <summary>One email to one recipient, with a plain-text and an HTML body.</summary>
public sealed record EmailMessage(string ToEmail, string ToName, string Subject, string TextBody, string HtmlBody);
