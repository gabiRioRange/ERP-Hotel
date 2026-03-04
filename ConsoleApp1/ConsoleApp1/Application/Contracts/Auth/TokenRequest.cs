namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record TokenRequest(string ClientId, string ClientSecret, string Scope);