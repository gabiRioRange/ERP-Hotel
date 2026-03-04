namespace ConsoleApp1.Application.Contracts.Auth;

public sealed record SecurityAuditLogQueryResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<SecurityAuditLogItemResponse> Items);