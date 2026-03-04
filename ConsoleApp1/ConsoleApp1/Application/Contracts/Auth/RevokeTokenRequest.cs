namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record RevokeTokenRequest(string ClientId, string ClientSecret, string RefreshToken);