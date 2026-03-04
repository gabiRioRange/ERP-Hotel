using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ConsoleApp1.Application.Observability;

public static class HotelTelemetry
{
    public const string ServiceName = "hotel-erp-api";
    public const string ServiceVersion = "1.0.0";

    public static readonly ActivitySource ActivitySource = new(ServiceName);
    public static readonly Meter Meter = new(ServiceName);

    public static readonly Counter<long> ReservationCreatedCounter =
        Meter.CreateCounter<long>("hotel.reservations.created", unit: "reservations");

    public static readonly Counter<long> ReservationRejectedCounter =
        Meter.CreateCounter<long>("hotel.reservations.rejected", unit: "reservations");

    public static readonly Histogram<double> AvailabilityQueryDurationMs =
        Meter.CreateHistogram<double>("hotel.availability.query.duration", unit: "ms");

    public static readonly Histogram<double> HousekeepingRouteDurationMs =
        Meter.CreateHistogram<double>("hotel.housekeeping.route.duration", unit: "ms");

    public static readonly Histogram<long> HousekeepingRouteSize =
        Meter.CreateHistogram<long>("hotel.housekeeping.route.size", unit: "rooms");
}