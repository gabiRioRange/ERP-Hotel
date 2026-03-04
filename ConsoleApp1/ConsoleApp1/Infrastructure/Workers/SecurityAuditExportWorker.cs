using ConsoleApp1.Application.Services;

namespace ConsoleApp1.Infrastructure.Workers;

public class SecurityAuditExportWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SecurityAuditExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        try
        {
            await ProcessPendingJobsSafelyAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingJobsSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("SecurityAuditExportWorker finalizado por cancelamento.");
        }
    }

    private async Task ProcessPendingJobsSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var securityAuditService = scope.ServiceProvider.GetRequiredService<SecurityAuditService>();
            var processed = await securityAuditService.ProcessPendingExportJobsAsync(cancellationToken);

            if (processed > 0)
            {
                logger.LogInformation("Security audit export worker processou {Count} job(s).", processed);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Processamento do worker de exportação cancelado.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro no worker de exportação de auditoria.");
        }
    }
}