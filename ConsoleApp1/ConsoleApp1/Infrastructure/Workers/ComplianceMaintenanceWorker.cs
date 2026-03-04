using ConsoleApp1.Application.Contracts.Auth;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConsoleApp1.Infrastructure.Workers;

public class ComplianceMaintenanceWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ComplianceOptions> complianceOptionsAccessor,
    ILogger<ComplianceMaintenanceWorker> logger) : BackgroundService
{
    private readonly ComplianceOptions _options = complianceOptionsAccessor.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));

        try
        {
            await RunCycleSafelyAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCycleSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ComplianceMaintenanceWorker finalizado por cancelamento.");
        }
    }

    private async Task RunCycleSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RunCycleAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Ciclo de compliance cancelado.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Erro no ciclo de manutenção de compliance.");
        }
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotelDbContext>();

        var now = DateTime.UtcNow;
        var anonymizeBefore = now.AddDays(-_options.IpAnonymizationDays);
        var deleteBefore = now.AddDays(-_options.SecurityAuditRetentionDays);

        var logsToAnonymize = await dbContext.SecurityAuditLogs
            .Where(log => log.OccurredUtc <= anonymizeBefore && log.IpAddress != null)
            .ToListAsync(cancellationToken);

        foreach (var log in logsToAnonymize)
        {
            log.IpAddress = "anonymized";
        }

        var expiredAuditLogs = await dbContext.SecurityAuditLogs
            .Where(log => log.OccurredUtc <= deleteBefore)
            .ToListAsync(cancellationToken);

        var expiredExportLogs = await dbContext.SecurityExportAuditLogs
            .Where(log => log.OccurredUtc <= deleteBefore)
            .ToListAsync(cancellationToken);

        var expiredJobs = await dbContext.SecurityAuditExportJobs
            .Where(job => job.CreatedUtc <= deleteBefore)
            .ToListAsync(cancellationToken);

        dbContext.SecurityAuditLogs.RemoveRange(expiredAuditLogs);
        dbContext.SecurityExportAuditLogs.RemoveRange(expiredExportLogs);
        dbContext.SecurityAuditExportJobs.RemoveRange(expiredJobs);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Compliance maintenance executada. Anonymized={Anonymized}; DeletedAuditLogs={AuditDeleted}; DeletedExportLogs={ExportDeleted}; DeletedJobs={JobsDeleted}",
            logsToAnonymize.Count,
            expiredAuditLogs.Count,
            expiredExportLogs.Count,
            expiredJobs.Count);
    }
}