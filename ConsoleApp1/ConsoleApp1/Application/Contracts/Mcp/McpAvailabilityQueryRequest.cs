namespace ConsoleApp1.Application.Contracts.Mcp;

public sealed record McpAvailabilityQueryRequest(
    DateOnly CheckIn,
    DateOnly CheckOut,
    string? RoomType
);