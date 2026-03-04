using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ConsoleApp1.Infrastructure.Health;

public class OtlpHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var endpointRaw = configuration["OpenTelemetry:Otlp:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpointRaw) || !Uri.TryCreate(endpointRaw, UriKind.Absolute, out var endpoint))
        {
            return HealthCheckResult.Degraded("OTLP endpoint não configurado.");
        }

        var port = endpoint.Port > 0 ? endpoint.Port : endpoint.Scheme == "https" ? 443 : 80;

        try
        {
            using var tcpClient = new System.Net.Sockets.TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));
            await tcpClient.ConnectAsync(endpoint.Host, port, timeoutCts.Token);
            return HealthCheckResult.Healthy("OTLP endpoint alcançável.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Falha ao conectar no OTLP endpoint.", exception);
        }
    }
}