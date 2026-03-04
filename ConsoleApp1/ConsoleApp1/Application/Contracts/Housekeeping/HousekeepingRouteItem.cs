namespace ConsoleApp1.Application.Contracts.Housekeeping;

public sealed record HousekeepingRouteItem(
    Guid RoomId,
    string RoomNumber,
    int Floor,
    int Priority,
    string Reason
);