namespace ConsoleApp1.Application.Contracts.Mcp;

public sealed class McpQuotaOptions
{
    public int RequestsPerMinutePerClient { get; set; } = 60;
}