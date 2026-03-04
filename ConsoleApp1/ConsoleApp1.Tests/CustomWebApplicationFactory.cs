using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ConsoleApp1.Tests;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, "LocalInMemory");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["LocalExecution:Profile"] = "local-inmemory-tests",
                ["LocalExecution:PreferredDatabaseProvider"] = "inmemory",
                ["LocalExecution:EnableDatabaseFallback"] = "true",
                ["LocalExecution:InMemoryDatabaseName"] = $"hotel-erp-tests-{Guid.NewGuid():N}",
                ["ConnectionStrings:HotelDb"] = string.Empty,
                ["OpenTelemetry:Otlp:Endpoint"] = string.Empty
            };

            configBuilder.AddInMemoryCollection(overrides);
        });
    }
}
