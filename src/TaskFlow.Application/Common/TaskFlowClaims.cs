namespace TaskFlow.Application.Common;

/// <summary>Custom claims in TaskFlow access tokens (the standard ones use <c>ClaimTypes</c>).</summary>
public static class TaskFlowClaims
{
    /// <summary>The user's <c>SecurityStamp</c> when the token was issued. A token whose stamp no longer matches the
    /// database (password or role changed since) is rejected.</summary>
    public const string SecurityStamp = "security_stamp";
}
