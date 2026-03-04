using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp1.Infrastructure.Persistence;

public static class HotelSeedData
{
    public static async Task SeedAsync(HotelDbContext dbContext)
    {
        if (await dbContext.Rooms.AnyAsync())
        {
            return;
        }

        var rooms = new[]
        {
            new Room { Number = "101", Type = "standard", Floor = 1, Status = RoomStatus.Available },
            new Room { Number = "102", Type = "standard", Floor = 1, Status = RoomStatus.Dirty },
            new Room { Number = "201", Type = "luxo", Floor = 2, Status = RoomStatus.Available },
            new Room { Number = "202", Type = "suite", Floor = 2, Status = RoomStatus.Occupied },
            new Room { Number = "301", Type = "suite", Floor = 3, Status = RoomStatus.OutOfService }
        };

        await dbContext.Rooms.AddRangeAsync(rooms);
        await dbContext.SaveChangesAsync();
    }
}