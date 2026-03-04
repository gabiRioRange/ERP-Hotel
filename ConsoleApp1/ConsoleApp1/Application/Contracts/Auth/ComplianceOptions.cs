namespace ConsoleApp1.Application.Contracts.Auth;

public sealed class ComplianceOptions
{
    public int SecurityAuditRetentionDays { get; set; } = 365;
    public int IpAnonymizationDays { get; set; } = 30;
}