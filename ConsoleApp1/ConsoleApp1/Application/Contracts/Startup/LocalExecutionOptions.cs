namespace ConsoleApp1.Application.Contracts.Startup;

public sealed class LocalExecutionOptions
{
    public string Profile { get; set; } = "local-inmemory";
    public string PreferredDatabaseProvider { get; set; } = "inmemory";
    public bool EnableDatabaseFallback { get; set; } = true;
    public string InMemoryDatabaseName { get; set; } = "hotel-erp-local";
}
