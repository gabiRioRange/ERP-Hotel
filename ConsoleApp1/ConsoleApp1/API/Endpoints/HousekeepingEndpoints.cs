using ConsoleApp1.Application.Services;

namespace ConsoleApp1.API.Endpoints;

public static class HousekeepingEndpoints
{
    public static IEndpointRouteBuilder MapHousekeepingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/housekeeping")
            .WithTags("Housekeeping")
            .RequireAuthorization("OperationsRead");

        group.MapGet("/route", async (
            DateOnly? operationDate,
            HousekeepingRoutingService routingService,
            CancellationToken cancellationToken) =>
        {
            var date = operationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var route = await routingService.BuildDailyRouteAsync(date, cancellationToken);

            return Results.Ok(new
            {
                operationDate = date,
                generatedAtUtc = DateTime.UtcNow,
                route
            });
        });

        return app;
    }
}