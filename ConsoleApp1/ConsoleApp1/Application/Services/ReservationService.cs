using ConsoleApp1.Application.Contracts.Reservations;
using ConsoleApp1.Application.Observability;
using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Domain.Enums;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ConsoleApp1.Application.Services;

public class ReservationService(HotelDbContext dbContext, ILogger<ReservationService> logger)
{
    public async Task<IReadOnlyList<AvailabilityResponse>> GetAvailableRoomsAsync(
        DateOnly checkIn,
        DateOnly checkOut,
        string? roomType,
        CancellationToken cancellationToken = default)
    {
        using var activity = HotelTelemetry.ActivitySource.StartActivity("reservation.availability.query");
        activity?.SetTag("reservation.checkin", checkIn.ToString("O"));
        activity?.SetTag("reservation.checkout", checkOut.ToString("O"));
        activity?.SetTag("reservation.room_type", roomType ?? "any");
        var stopwatch = Stopwatch.StartNew();

        ValidateDates(checkIn, checkOut);

        var checkInUtc = DateTime.SpecifyKind(checkIn.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var checkOutUtc = DateTime.SpecifyKind(checkOut.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var roomsQuery = dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Status != RoomStatus.OutOfService)
            .Where(room =>
                !dbContext.Reservations.Any(reservation =>
                    reservation.RoomId == room.Id &&
                    (reservation.Status == ReservationStatus.Confirmed || reservation.Status == ReservationStatus.CheckedIn) &&
                    reservation.CheckInUtc < checkOutUtc &&
                    checkInUtc < reservation.CheckOutUtc));

        if (!string.IsNullOrWhiteSpace(roomType))
        {
            roomsQuery = roomsQuery.Where(room => room.Type == roomType);
        }

        var rooms = await roomsQuery
            .OrderBy(room => room.Floor)
            .ThenBy(room => room.Number)
            .Select(room => new AvailabilityResponse(
                room.Id,
                room.Number,
                room.Type,
                room.Floor,
                room.Status.ToString()))
            .ToListAsync(cancellationToken);

        stopwatch.Stop();
        HotelTelemetry.AvailabilityQueryDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("room.type", roomType ?? "any"));
        activity?.SetTag("availability.count", rooms.Count);

        logger.LogInformation(
            "Availability query completed for period {CheckIn} - {CheckOut}. RoomType={RoomType}; Count={Count}; DurationMs={DurationMs}",
            checkIn,
            checkOut,
            roomType ?? "any",
            rooms.Count,
            stopwatch.Elapsed.TotalMilliseconds);

        return rooms;
    }

    public async Task<ReservationResponse> CreateReservationAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = HotelTelemetry.ActivitySource.StartActivity("reservation.create");
        activity?.SetTag("reservation.room_id", request.RoomId);
        activity?.SetTag("reservation.source_channel", request.SourceChannel);

        ValidateDates(request.CheckIn, request.CheckOut);

        var room = await dbContext.Rooms
            .SingleOrDefaultAsync(currentRoom => currentRoom.Id == request.RoomId, cancellationToken);

        if (room is null)
        {
            HotelTelemetry.ReservationRejectedCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "room-not-found"));
            throw new InvalidOperationException("Quarto não encontrado.");
        }

        if (room.Status == RoomStatus.OutOfService)
        {
            HotelTelemetry.ReservationRejectedCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "room-out-of-service"));
            throw new InvalidOperationException("Quarto indisponível para reserva.");
        }

        var availableRooms = await GetAvailableRoomsAsync(
            request.CheckIn,
            request.CheckOut,
            room.Type,
            cancellationToken);

        if (availableRooms.All(availableRoom => availableRoom.RoomId != room.Id))
        {
            HotelTelemetry.ReservationRejectedCounter.Add(1,
                new KeyValuePair<string, object?>("reason", "inventory-conflict"));
            throw new InvalidOperationException("Conflito de inventário: quarto já reservado no período solicitado.");
        }

        var reservation = new Reservation
        {
            RoomId = request.RoomId,
            GuestFullName = request.GuestFullName.Trim(),
            CheckInUtc = DateTime.SpecifyKind(request.CheckIn.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            CheckOutUtc = DateTime.SpecifyKind(request.CheckOut.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            EarlyCheckInRequested = request.EarlyCheckInRequested,
            SourceChannel = string.IsNullOrWhiteSpace(request.SourceChannel) ? "direct" : request.SourceChannel.Trim().ToLowerInvariant(),
            Status = ReservationStatus.Confirmed,
            CreatedUtc = DateTime.UtcNow
        };

        await dbContext.Reservations.AddAsync(reservation, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        HotelTelemetry.ReservationCreatedCounter.Add(1,
            new KeyValuePair<string, object?>("room.type", room.Type),
            new KeyValuePair<string, object?>("channel", reservation.SourceChannel));
        activity?.SetTag("reservation.id", reservation.Id);

        logger.LogInformation(
            "Reservation created: ReservationId={ReservationId}; RoomId={RoomId}; CheckIn={CheckIn}; CheckOut={CheckOut}; Channel={Channel}",
            reservation.Id,
            reservation.RoomId,
            request.CheckIn,
            request.CheckOut,
            reservation.SourceChannel);

        return new ReservationResponse(
            reservation.Id,
            reservation.RoomId,
            reservation.GuestFullName,
            request.CheckIn,
            request.CheckOut,
            reservation.Status.ToString());
    }

    private static void ValidateDates(DateOnly checkIn, DateOnly checkOut)
    {
        if (checkOut <= checkIn)
        {
            throw new ArgumentException("A data de check-out deve ser maior que a data de check-in.");
        }
    }
}