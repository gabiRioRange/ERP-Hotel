namespace ConsoleApp1.Application.Contracts.Reservations;

public sealed record AvailabilityResponse(
    Guid RoomId,
    string Number,
    string Type,
    int Floor,
    string Status
);