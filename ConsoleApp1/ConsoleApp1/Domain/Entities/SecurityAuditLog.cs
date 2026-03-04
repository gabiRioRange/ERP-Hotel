namespace ConsoleApp1.Domain.Entities;

public class SecurityAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? TraceId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
}