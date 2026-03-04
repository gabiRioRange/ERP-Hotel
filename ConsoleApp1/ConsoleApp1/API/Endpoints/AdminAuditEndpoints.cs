using ConsoleApp1.Application.Services;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace ConsoleApp1.API.Endpoints;

public static class AdminAuditEndpoints
{
    public static IEndpointRouteBuilder MapAdminAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization("AuditRead");

        group.MapGet("/security-audit-logs", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            string? eventType,
            bool? success,
            string? clientId,
            int page,
            int pageSize,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            {
                return Results.BadRequest(new { error = "O parâmetro fromUtc deve ser menor ou igual ao toUtc." });
            }

            var result = await securityAuditService.QueryAsync(
                fromUtc,
                toUtc,
                eventType,
                success,
                clientId,
                page == 0 ? 1 : page,
                pageSize == 0 ? 20 : pageSize,
                cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/security-audit-logs/cursor", async (
            string? cursor,
            int limit,
            DateTime? fromUtc,
            DateTime? toUtc,
            string? eventType,
            bool? success,
            string? clientId,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            {
                return Results.Problem(
                    title: "Parâmetros inválidos",
                    detail: "O parâmetro fromUtc deve ser menor ou igual ao toUtc.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var result = await securityAuditService.QueryByCursorAsync(
                cursor,
                limit == 0 ? 50 : limit,
                fromUtc,
                toUtc,
                eventType,
                success,
                clientId,
                cancellationToken);

            return Results.Ok(new { items = result.Items, nextCursor = result.NextCursor });
        });

        group.MapGet("/security-audit-logs/export.csv", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            string? eventType,
            bool? success,
            string? clientId,
            int maxRows,
            bool gzip,
            string? filename,
            HttpContext httpContext,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            {
                return Results.Problem(
                    title: "Parâmetros inválidos",
                    detail: "O parâmetro fromUtc deve ser menor ou igual ao toUtc.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var items = await securityAuditService.QueryForExportAsync(
                fromUtc,
                toUtc,
                eventType,
                success,
                clientId,
                maxRows == 0 ? 5000 : maxRows,
                cancellationToken);

            var bytes = securityAuditService.BuildCsvBytes(items);
            var sha256 = SecurityAuditService.ComputeSha256(bytes);
            var baseName = SanitizeFileName(filename) ?? $"security-audit-logs-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var baseFileName = $"{baseName}.csv";
            var requestedBy = httpContext.User.FindFirst("sub")?.Value ?? "unknown";
            var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

            await securityAuditService.RegisterExportAuditAsync(
                requestedBy,
                fromUtc,
                toUtc,
                eventType,
                success,
                clientId,
                items.Count,
                sha256,
                traceId,
                cancellationToken);

            httpContext.Response.Headers.Append("X-File-SHA256", sha256);
            httpContext.Response.Headers.Append("X-Export-Rows", items.Count.ToString());

            if (gzip)
            {
                await using var output = new MemoryStream();
                await using (var gzipStream = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
                {
                    await gzipStream.WriteAsync(bytes, cancellationToken);
                }

                return Results.File(output.ToArray(), "application/gzip", $"{baseFileName}.gz");
            }

            return Results.File(bytes, "text/csv; charset=utf-8", baseFileName);
        });

        group.MapPost("/security-audit-logs/export-jobs", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            string? eventType,
            bool? success,
            string? clientId,
            HttpContext httpContext,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            if (fromUtc.HasValue && toUtc.HasValue && fromUtc > toUtc)
            {
                return Results.Problem(
                    title: "Parâmetros inválidos",
                    detail: "O parâmetro fromUtc deve ser menor ou igual ao toUtc.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var requestedBy = httpContext.User.FindFirst("sub")?.Value ?? "unknown";
            var jobId = await securityAuditService.CreateExportJobAsync(
                requestedBy,
                fromUtc,
                toUtc,
                eventType,
                success,
                clientId,
                cancellationToken);

            return Results.Accepted($"/api/v1/admin/security-audit-logs/export-jobs/{jobId}", new { jobId, status = "pending" });
        });

        group.MapGet("/security-audit-logs/export-jobs/{jobId:guid}", async (
            Guid jobId,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            var job = await securityAuditService.GetExportJobAsync(jobId, cancellationToken);
            if (job is null)
            {
                return Results.Problem(
                    title: "Job não encontrado",
                    detail: "Não existe export job com o identificador informado.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(new
            {
                job.Id,
                job.Status,
                job.CreatedUtc,
                job.CompletedUtc,
                job.FileName,
                job.Sha256
            });
        });

        group.MapGet("/security-audit-logs/export-jobs/{jobId:guid}/download", async (
            Guid jobId,
            SecurityAuditService securityAuditService,
            CancellationToken cancellationToken) =>
        {
            var job = await securityAuditService.GetExportJobAsync(jobId, cancellationToken);
            if (job is null)
            {
                return Results.Problem(
                    title: "Job não encontrado",
                    detail: "Não existe export job com o identificador informado.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            if (job.Status != "completed" || job.Payload is null)
            {
                return Results.Problem(
                    title: "Exportação em processamento",
                    detail: "O arquivo ainda não está pronto para download.",
                    statusCode: StatusCodes.Status409Conflict);
            }

            return Results.File(job.Payload, job.ContentType ?? "text/csv; charset=utf-8", job.FileName ?? $"audit-export-{job.Id}.csv");
        });

        return app;
    }

    private static string? SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var sanitized = new string(value.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' ).ToArray());
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return null;
        }

        return sanitized;
    }
}