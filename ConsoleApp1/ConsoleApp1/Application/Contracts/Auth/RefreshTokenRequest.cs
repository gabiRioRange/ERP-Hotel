namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record RefreshTokenRequest(string ClientId, string ClientSecret, string RefreshToken);