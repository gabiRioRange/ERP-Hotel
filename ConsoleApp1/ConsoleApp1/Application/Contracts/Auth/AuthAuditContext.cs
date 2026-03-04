namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record AuthAuditContext(string? TraceId, string? IpAddress);