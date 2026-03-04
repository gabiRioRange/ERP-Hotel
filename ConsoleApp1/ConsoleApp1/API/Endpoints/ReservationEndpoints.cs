using ConsoleApp1.Application.Contracts.Reservations;
using ConsoleApp1.Application.Services;
using System.Text;
using System.Text.Json;

namespace ConsoleApp1.API.Endpoints;

public static class ReservationEndpoints
{
    public static IEndpointRouteBuilder MapReservationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/reservations")
            .WithTags("Reservations")
            .RequireAuthorization();

        group.MapPost("", async (
            CreateReservationRequest request,
            HttpContext httpContext,
            IdempotencyService idempotencyService,
            ReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.Problem(
                    title: "Idempotency key obrigatória",
                    detail: "Envie o header Idempotency-Key para criação segura de reservas.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var requestPayload = JsonSerializer.Serialize(request);
            var requestHash = IdempotencyService.ComputeRequestHash(requestPayload);
            var existing = await idempotencyService.GetAsync("reservation.create", idempotencyKey, cancellationToken);

            if (existing is not null)
            {
                if (existing.RequestHash != requestHash)
                {
                    return Results.Problem(
                        title: "Conflito de idempotência",
                        detail: "A mesma Idempotency-Key foi utilizada com payload diferente.",
                        statusCode: StatusCodes.Status409Conflict);
                }

                return Results.Content(
                    existing.ResponseJson,
                    "application/json",
                    Encoding.UTF8,
                    existing.StatusCode);
            }

            try
            {
                var reservation = await reservationService.CreateReservationAsync(request, cancellationToken);
                var responseJson = JsonSerializer.Serialize(reservation);
                await idempotencyService.SaveAsync("reservation.create", idempotencyKey, requestHash, StatusCodes.Status201Created, responseJson, cancellationToken);
                return Results.Created($"/api/v1/reservations/{reservation.ReservationId}", reservation);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return Results.Problem(
                    title: "Falha ao criar reserva",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization("ReservationsWrite");

        group.MapGet("/availability", async (
            DateOnly checkIn,
            DateOnly checkOut,
            string? roomType,
            ReservationService reservationService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var rooms = await reservationService.GetAvailableRoomsAsync(
                    checkIn,
                    checkOut,
                    roomType,
                    cancellationToken);

                return Results.Ok(rooms);
            }
            catch (ArgumentException exception)
            {
                return Results.Problem(
                    title: "Parâmetros inválidos",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status400BadRequest);
            }
        }).RequireAuthorization("OperationsRead");

        return app;
    }
}