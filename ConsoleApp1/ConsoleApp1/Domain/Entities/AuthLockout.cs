namespace ConsoleApp1.Domain.Entities;

public class AuthLockout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTime LastFailedUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}