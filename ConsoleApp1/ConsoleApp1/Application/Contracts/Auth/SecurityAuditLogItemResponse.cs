namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record SecurityAuditLogItemResponse(
    Guid Id,
    string EventType,
    bool Success,
    string ClientId,
    string? Reason,
    string? TraceId,
    string? IpAddress,
    DateTime OccurredUtc);