namespace ConsoleApp1.Domain.Entities;

public class SecurityExportAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestedBy { get; set; } = string.Empty;
    public string? ClientIdFilter { get; set; }
    public string? EventTypeFilter { get; set; }
    public bool? SuccessFilter { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int RowsExported { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
}