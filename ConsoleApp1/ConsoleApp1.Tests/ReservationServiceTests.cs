using ConsoleApp1.Application.Contracts.Reservations;
using ConsoleApp1.Application.Services;
using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Domain.Enums;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ConsoleApp1.Tests;

public class ReservationServiceTests
{
    [Fact]
    public async Task GetAvailableRoomsAsync_Throws_WhenCheckoutIsBeforeCheckin()
    {
        await using var dbContext = CreateDbContext();
        var service = new ReservationService(dbContext, NullLogger<ReservationService>.Instance);

        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var checkOut = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        var action = () => service.GetAvailableRoomsAsync(checkIn, checkOut, null);

        await Assert.ThrowsAsync<ArgumentException>(action);
    }

    [Fact]
    public async Task CreateReservationAsync_RejectsOutOfServiceRoom()
    {
        await using var dbContext = CreateDbContext();
        var outOfServiceRoom = new Room
        {
            Number = "999",
            Type = "suite",
            Floor = 9,
            Status = RoomStatus.OutOfService
        };

        dbContext.Rooms.Add(outOfServiceRoom);
        await dbContext.SaveChangesAsync();

        var service = new ReservationService(dbContext, NullLogger<ReservationService>.Instance);
        var request = new CreateReservationRequest(
            outOfServiceRoom.Id,
            "Teste Unidade",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            false,
            "direct");

        var action = () => service.CreateReservationAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    private static HotelDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HotelDbContext>()
            .UseInMemoryDatabase($"reservation-service-tests-{Guid.NewGuid():N}")
            .Options;

        return new HotelDbContext(options);
    }
}
