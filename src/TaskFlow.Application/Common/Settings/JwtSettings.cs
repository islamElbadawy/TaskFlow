namespace TaskFlow.Application.Common.Settings;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public int ExpirationInMinutes { get; set; } = 1440; // 24 hours
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
}