namespace ConsoleApp1.Application.Contracts.Auth;

public sealed class AuthClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public int RefreshTokenDays { get; set; } = 14;
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}