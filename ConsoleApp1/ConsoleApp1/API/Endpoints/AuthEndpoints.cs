using ConsoleApp1.Application.Contracts.Auth;
using ConsoleApp1.Application.Services;
using System.Diagnostics;

namespace ConsoleApp1.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth");

        group.MapPost("/token", async (TokenRequest request, HttpContext httpContext, TokenService tokenService, CancellationToken cancellationToken) =>
        {
            var auditContext = BuildAuditContext(httpContext);
            var token = await tokenService.IssueAsync(request, auditContext, cancellationToken);

            if (token is null)
            {
                return Results.Problem(
                    title: "Não autorizado",
                    detail: "Credenciais inválidas, cliente bloqueado ou escopo inválido.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(token);
        }).AllowAnonymous();

        group.MapPost("/refresh", async (RefreshTokenRequest request, HttpContext httpContext, TokenService tokenService, CancellationToken cancellationToken) =>
        {
            var auditContext = BuildAuditContext(httpContext);
            var token = await tokenService.RefreshAsync(request, auditContext, cancellationToken);

            if (token is null)
            {
                return Results.Problem(
                    title: "Não autorizado",
                    detail: "Refresh token inválido, expirado ou cliente bloqueado.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            return Results.Ok(token);
        }).AllowAnonymous();

        group.MapPost("/revoke", async (RevokeTokenRequest request, HttpContext httpContext, TokenService tokenService, CancellationToken cancellationToken) =>
        {
            var auditContext = BuildAuditContext(httpContext);
            var revoked = await tokenService.RevokeAsync(request, auditContext, cancellationToken);
            if (!revoked)
            {
                return Results.Problem(
                    title: "Token não encontrado",
                    detail: "Refresh token não encontrado.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.NoContent();
        }).AllowAnonymous();

        return app;
    }

    private static AuthAuditContext BuildAuditContext(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        var ip = string.IsNullOrWhiteSpace(forwardedFor)
            ? context.Connection.RemoteIpAddress?.ToString()
            : forwardedFor.Split(',').FirstOrDefault()?.Trim();

        return new AuthAuditContext(traceId, ip);
    }
}