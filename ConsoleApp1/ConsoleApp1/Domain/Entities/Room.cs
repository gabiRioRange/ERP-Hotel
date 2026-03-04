using ConsoleApp1.Domain.Enums;

namespace ConsoleApp1.Domain.Entities;

public class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Number { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Floor { get; set; }
    public RoomStatus Status { get; set; } = RoomStatus.Available;
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}