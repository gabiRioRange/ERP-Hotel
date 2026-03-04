using ConsoleApp1.API.Filters;
using ConsoleApp1.Application.Contracts.Mcp;
using ConsoleApp1.Application.Services;

namespace ConsoleApp1.API.Endpoints;

public static class McpEndpoints
{
    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/mcp")
            .WithTags("MCP");

        group.MapGet("/tools", () => Results.Ok(new
        {
            tools = new object[]
            {
                new
                {
                    name = "hotel.queryAvailability",
                    description = "Consulta disponibilidade de quartos em um intervalo de datas.",
                    requiredScope = "mcp.availability.query",
                    rateLimit = "per-client-per-minute",
                    input = new { checkIn = "date", checkOut = "date", roomType = "string?" }
                },
                new
                {
                    name = "hotel.getHousekeepingRoute",
                    description = "Retorna rota dinâmica de limpeza para operação diária.",
                    requiredScope = "mcp.housekeeping.read",
                    rateLimit = "per-client-per-minute",
                    input = new { operationDate = "date?" }
                }
            }
        }))
        .RequireAuthorization("McpToolsRead")
        .AddEndpointFilter<McpQuotaFilter>();

        group.MapPost("/query-availability", async (
            McpAvailabilityQueryRequest request,
            ReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var rooms = await reservationService.GetAvailableRoomsAsync(
                    request.CheckIn,
                    request.CheckOut,
                    request.RoomType,
                    cancellationToken);

                return Results.Ok(new
                {
                    interval = new { request.CheckIn, request.CheckOut },
                    request.RoomType,
                    availableRooms = rooms,
                    total = rooms.Count
                });
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Parâmetros inválidos",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .RequireAuthorization("McpAvailabilityQuery")
        .AddEndpointFilter<McpQuotaFilter>();

        group.MapGet("/housekeeping-route", async (
            DateOnly? operationDate,
            HousekeepingRoutingService routingService,
            CancellationToken cancellationToken) =>
        {
            var date = operationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var route = await routingService.BuildDailyRouteAsync(date, cancellationToken);
            return Results.Ok(new { operationDate = date, route, total = route.Count });
        })
        .RequireAuthorization("McpHousekeepingRead")
        .AddEndpointFilter<McpQuotaFilter>();

        return app;
    }
}