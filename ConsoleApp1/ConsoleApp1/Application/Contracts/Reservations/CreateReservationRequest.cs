namespace ConsoleApp1.Application.Contracts.Reservations;

public sealed record CreateReservationRequest(
    Guid RoomId,
    string GuestFullName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    bool EarlyCheckInRequested,
    string SourceChannel
);