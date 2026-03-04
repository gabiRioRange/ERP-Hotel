using ConsoleApp1.Domain.Enums;

namespace ConsoleApp1.Domain.Entities;

public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
    public string GuestFullName { get; set; } = string.Empty;
    public DateTime CheckInUtc { get; set; }
    public DateTime CheckOutUtc { get; set; }
    public bool EarlyCheckInRequested { get; set; }
    public string SourceChannel { get; set; } = "direct";
    public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}