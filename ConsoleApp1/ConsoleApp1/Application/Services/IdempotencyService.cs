using System.Security.Cryptography;
using System.Text;
using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ConsoleApp1.Application.Services;

public class IdempotencyService(HotelDbContext dbContext)
{
    public async Task<IdempotencyRecord?> GetAsync(string scope, string key, CancellationToken cancellationToken = default)
    {
        return await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Scope == scope && record.Key == key, cancellationToken);
    }

    public static string ComputeRequestHash(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    public async Task SaveAsync(
        string scope,
        string key,
        string requestHash,
        int statusCode,
        string responseJson,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.IdempotencyRecords
            .SingleOrDefaultAsync(record => record.Scope == scope && record.Key == key, cancellationToken);

        if (existing is not null)
        {
            existing.RequestHash = requestHash;
            existing.StatusCode = statusCode;
            existing.ResponseJson = responseJson;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var record = new IdempotencyRecord
        {
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            StatusCode = statusCode,
            ResponseJson = responseJson,
            CreatedUtc = DateTime.UtcNow
        };

        await dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}