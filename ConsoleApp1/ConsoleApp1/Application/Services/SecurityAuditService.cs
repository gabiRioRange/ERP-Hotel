using ConsoleApp1.Application.Contracts.Auth;
using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ConsoleApp1.Application.Services;

public class SecurityAuditService(HotelDbContext dbContext, ILogger<SecurityAuditService> logger)
{
    public async Task WriteAsync(
        string eventType,
        bool success,
        string clientId,
        string? reason,
        AuthAuditContext context,
        CancellationToken cancellationToken = default)
    {
        var log = new SecurityAuditLog
        {
            EventType = eventType,
            Success = success,
            ClientId = clientId,
            Reason = reason,
            TraceId = context.TraceId,
            IpAddress = context.IpAddress,
            OccurredUtc = DateTime.UtcNow
        };

        await dbContext.SecurityAuditLogs.AddAsync(log, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Security audit event registered: EventType={EventType}; Success={Success}; ClientId={ClientId}; TraceId={TraceId}; Ip={IpAddress}",
            eventType,
            success,
            clientId,
            context.TraceId,
            context.IpAddress);
    }

    public async Task<SecurityAuditLogQueryResponse> QueryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        bool? success,
        string? clientId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize switch
        {
            < 1 => 20,
            > 200 => 200,
            _ => pageSize
        };

        var query = dbContext.SecurityAuditLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.OccurredUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.OccurredUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalized = eventType.Trim();
            query = query.Where(log => log.EventType == normalized);
        }

        if (success.HasValue)
        {
            query = query.Where(log => log.Success == success.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var normalized = clientId.Trim();
            query = query.Where(log => log.ClientId == normalized);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(log => log.OccurredUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(log => new SecurityAuditLogItemResponse(
                log.Id,
                log.EventType,
                log.Success,
                log.ClientId,
                log.Reason,
                log.TraceId,
                log.IpAddress,
                log.OccurredUtc))
            .ToListAsync(cancellationToken);

        return new SecurityAuditLogQueryResponse(page, pageSize, total, items);
    }

    public async Task<(IReadOnlyList<SecurityAuditLogItemResponse> Items, string? NextCursor)> QueryByCursorAsync(
        string? cursor,
        int limit,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        bool? success,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        limit = limit switch
        {
            < 1 => 50,
            > 200 => 200,
            _ => limit
        };

        var query = BuildFilteredQuery(fromUtc, toUtc, eventType, success, clientId);
        var cursorInfo = ParseCursor(cursor);

        if (cursorInfo is not null)
        {
            query = query.Where(log =>
                log.OccurredUtc < cursorInfo.Value.OccurredUtc ||
                (log.OccurredUtc == cursorInfo.Value.OccurredUtc && string.CompareOrdinal(log.Id.ToString(), cursorInfo.Value.Id) < 0));
        }

        var items = await query
            .OrderByDescending(log => log.OccurredUtc)
            .ThenByDescending(log => log.Id)
            .Take(limit)
            .Select(log => new SecurityAuditLogItemResponse(
                log.Id,
                log.EventType,
                log.Success,
                log.ClientId,
                log.Reason,
                log.TraceId,
                log.IpAddress,
                log.OccurredUtc))
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (items.Count == limit)
        {
            var last = items[^1];
            var raw = $"{last.OccurredUtc:O}|{last.Id}";
            nextCursor = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        return (items, nextCursor);
    }

    public async Task<IReadOnlyList<SecurityAuditLogItemResponse>> QueryForExportAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        bool? success,
        string? clientId,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        maxRows = maxRows switch
        {
            < 1 => 1000,
            > 10000 => 10000,
            _ => maxRows
        };

        var query = BuildFilteredQuery(fromUtc, toUtc, eventType, success, clientId);

        return await query
            .OrderByDescending(log => log.OccurredUtc)
            .Take(maxRows)
            .Select(log => new SecurityAuditLogItemResponse(
                log.Id,
                log.EventType,
                log.Success,
                log.ClientId,
                log.Reason,
                log.TraceId,
                log.IpAddress,
                log.OccurredUtc))
            .ToListAsync(cancellationToken);
    }

    public byte[] BuildCsvBytes(IReadOnlyList<SecurityAuditLogItemResponse> items)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("id,eventType,success,clientId,reason,traceId,ipAddress,occurredUtc");

        foreach (var item in items)
        {
            csvBuilder.AppendLine(string.Join(',',
                EscapeCsv(item.Id.ToString()),
                EscapeCsv(item.EventType),
                EscapeCsv(item.Success.ToString().ToLowerInvariant()),
                EscapeCsv(item.ClientId),
                EscapeCsv(item.Reason),
                EscapeCsv(item.TraceId),
                EscapeCsv(item.IpAddress),
                EscapeCsv(item.OccurredUtc.ToString("O"))));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csvBuilder.ToString())).ToArray();
    }

    public static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public async Task RegisterExportAuditAsync(
        string requestedBy,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        bool? success,
        string? clientId,
        int rows,
        string sha256,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var exportLog = new SecurityExportAuditLog
        {
            RequestedBy = requestedBy,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EventTypeFilter = eventType,
            SuccessFilter = success,
            ClientIdFilter = clientId,
            RowsExported = rows,
            Sha256 = sha256,
            TraceId = traceId,
            OccurredUtc = DateTime.UtcNow
        };

        await dbContext.SecurityExportAuditLogs.AddAsync(exportLog, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateExportJobAsync(
        string requestedBy,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        bool? success,
        string? clientId,
        CancellationToken cancellationToken = default)
    {
        var job = new SecurityAuditExportJob
        {
            RequestedBy = requestedBy,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EventTypeFilter = eventType,
            SuccessFilter = success,
            ClientIdFilter = clientId,
            Status = "pending",
            CreatedUtc = DateTime.UtcNow
        };

        await dbContext.SecurityAuditExportJobs.AddAsync(job, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }

    public async Task<int> ProcessPendingExportJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await dbContext.SecurityAuditExportJobs
            .Where(job => job.Status == "pending")
            .OrderBy(job => job.CreatedUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var job in jobs)
        {
            var items = await QueryForExportAsync(
                job.FromUtc,
                job.ToUtc,
                job.EventTypeFilter,
                job.SuccessFilter,
                job.ClientIdFilter,
                10000,
                cancellationToken);

            var bytes = BuildCsvBytes(items);
            var sha256 = ComputeSha256(bytes);

            job.Payload = bytes;
            job.ContentType = "text/csv; charset=utf-8";
            job.FileName = $"security-audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            job.Sha256 = sha256;
            job.Status = "completed";
            job.CompletedUtc = DateTime.UtcNow;
            processed += 1;

            await RegisterExportAuditAsync(
                job.RequestedBy,
                job.FromUtc,
                job.ToUtc,
                job.EventTypeFilter,
                job.SuccessFilter,
                job.ClientIdFilter,
                items.Count,
                sha256,
                "async-job",
                cancellationToken);
        }

        if (processed > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return processed;
    }

    public async Task<SecurityAuditExportJob?> GetExportJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SecurityAuditExportJobs.AsNoTracking().SingleOrDefaultAsync(job => job.Id == jobId, cancellationToken);
    }

    private IQueryable<SecurityAuditLog> BuildFilteredQuery(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? eventType,
        bool? success,
        string? clientId)
    {
        var query = dbContext.SecurityAuditLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(log => log.OccurredUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(log => log.OccurredUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var normalized = eventType.Trim();
            query = query.Where(log => log.EventType == normalized);
        }

        if (success.HasValue)
        {
            query = query.Where(log => log.Success == success.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var normalized = clientId.Trim();
            query = query.Where(log => log.ClientId == normalized);
        }

        return query;
    }

    private static (DateTime OccurredUtc, string Id)? ParseCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var split = decoded.Split('|');
            if (split.Length != 2)
            {
                return null;
            }

            if (!DateTime.TryParse(split[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var occurredUtc))
            {
                return null;
            }

            return (occurredUtc, split[1]);
        }
        catch
        {
            return null;
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}