namespace ConsoleApp1.Domain.Entities;

public class SecurityAuditExportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestedBy { get; set; } = string.Empty;
    public string? ClientIdFilter { get; set; }
    public string? EventTypeFilter { get; set; }
    public bool? SuccessFilter { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string Status { get; set; } = "pending";
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Payload { get; set; }
    public string? Sha256 { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
}