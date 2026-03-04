using ConsoleApp1.Application.Contracts.Housekeeping;
using ConsoleApp1.Application.Observability;
using ConsoleApp1.Domain.Enums;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ConsoleApp1.Application.Services;

public class HousekeepingRoutingService(HotelDbContext dbContext, ILogger<HousekeepingRoutingService> logger)
{
    public async Task<IReadOnlyList<HousekeepingRouteItem>> BuildDailyRouteAsync(
        DateOnly operationDate,
        CancellationToken cancellationToken = default)
    {
        using var activity = HotelTelemetry.ActivitySource.StartActivity("housekeeping.route.build");
        activity?.SetTag("housekeeping.operation_date", operationDate.ToString("O"));
        var stopwatch = Stopwatch.StartNew();

        var dayStart = DateTime.SpecifyKind(operationDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var checkOutStatuses = new[] { ReservationStatus.Confirmed, ReservationStatus.CheckedIn };

        var sameDayReservations = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.CheckInUtc >= dayStart && reservation.CheckInUtc < dayEnd)
            .ToListAsync(cancellationToken);

        var sameDayCheckOuts = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.CheckOutUtc >= dayStart &&
                reservation.CheckOutUtc < dayEnd &&
                checkOutStatuses.Contains(reservation.Status))
            .ToListAsync(cancellationToken);

        var targetRoomIds = sameDayReservations
            .Select(reservation => reservation.RoomId)
            .Concat(sameDayCheckOuts.Select(reservation => reservation.RoomId))
            .Distinct()
            .ToHashSet();

        var dirtyRooms = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => room.Status == RoomStatus.Dirty)
            .Select(room => room.Id)
            .ToListAsync(cancellationToken);

        foreach (var dirtyRoomId in dirtyRooms)
        {
            targetRoomIds.Add(dirtyRoomId);
        }

        var targetRooms = await dbContext.Rooms
            .AsNoTracking()
            .Where(room => targetRoomIds.Contains(room.Id))
            .ToListAsync(cancellationToken);

        var route = targetRooms
            .Select(room =>
            {
                var hasCheckoutToday = sameDayCheckOuts.Any(reservation => reservation.RoomId == room.Id);
                var hasSameDayArrival = sameDayReservations.Any(reservation => reservation.RoomId == room.Id);
                var hasEarlyCheckIn = sameDayReservations.Any(reservation => reservation.RoomId == room.Id && reservation.EarlyCheckInRequested);

                var priority = hasEarlyCheckIn ? 1
                    : hasCheckoutToday && hasSameDayArrival ? 1
                    : hasCheckoutToday ? 2
                    : room.Status == RoomStatus.Dirty ? 3
                    : 4;

                var reason = hasEarlyCheckIn ? "Early check-in solicitado"
                    : hasCheckoutToday && hasSameDayArrival ? "Checkout e check-in no mesmo dia"
                    : hasCheckoutToday ? "Checkout previsto hoje"
                    : room.Status == RoomStatus.Dirty ? "Quarto sujo pendente"
                    : "Manutenção preventiva";

                return new HousekeepingRouteItem(
                    room.Id,
                    room.Number,
                    room.Floor,
                    priority,
                    reason);
            })
            .OrderBy(item => item.Priority)
            .ThenBy(item => item.Floor)
            .ThenBy(item => item.RoomNumber)
            .ToList();

        stopwatch.Stop();
        HotelTelemetry.HousekeepingRouteDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
        HotelTelemetry.HousekeepingRouteSize.Record(route.Count);
        activity?.SetTag("housekeeping.route.size", route.Count);

        logger.LogInformation(
            "Housekeeping route generated for {OperationDate}. Rooms={Count}; DurationMs={DurationMs}",
            operationDate,
            route.Count,
            stopwatch.Elapsed.TotalMilliseconds);

        return route;
    }
}