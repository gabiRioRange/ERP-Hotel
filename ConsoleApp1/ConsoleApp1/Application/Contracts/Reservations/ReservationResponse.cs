namespace ConsoleApp1.Application.Contracts.Reservations;

public sealed record ReservationResponse(
    Guid ReservationId,
    Guid RoomId,
    string GuestFullName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    string Status
);