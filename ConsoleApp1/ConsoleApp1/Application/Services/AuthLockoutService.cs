using ConsoleApp1.Application.Contracts.Auth;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ConsoleApp1.Application.Services;

public class AuthLockoutService(HotelDbContext dbContext, IOptions<AuthClientOptions> authOptionsAccessor)
{
    private readonly AuthClientOptions _authOptions = authOptionsAccessor.Value;

    public async Task<bool> IsLockedAsync(string clientId, string ipAddress, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.AuthLockouts
            .AsNoTracking()
            .SingleOrDefaultAsync(lockout => lockout.ClientId == clientId && lockout.IpAddress == ipAddress, cancellationToken);

        return entry?.LockedUntilUtc is not null && entry.LockedUntilUtc > DateTime.UtcNow;
    }

    public async Task RegisterFailureAsync(string clientId, string ipAddress, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entry = await dbContext.AuthLockouts
            .SingleOrDefaultAsync(lockout => lockout.ClientId == clientId && lockout.IpAddress == ipAddress, cancellationToken);

        if (entry is null)
        {
            entry = new Domain.Entities.AuthLockout
            {
                ClientId = clientId,
                IpAddress = ipAddress,
                FailedAttempts = 1,
                LastFailedUtc = now
            };

            await dbContext.AuthLockouts.AddAsync(entry, cancellationToken);
        }
        else
        {
            if (entry.LastFailedUtc < now.AddMinutes(-_authOptions.LockoutMinutes))
            {
                entry.FailedAttempts = 0;
                entry.LockedUntilUtc = null;
            }

            entry.FailedAttempts += 1;
            entry.LastFailedUtc = now;
        }

        if (entry.FailedAttempts >= _authOptions.MaxFailedAttempts)
        {
            entry.LockedUntilUtc = now.AddMinutes(_authOptions.LockoutMinutes);
            entry.FailedAttempts = 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetAsync(string clientId, string ipAddress, CancellationToken cancellationToken = default)
    {
        var entry = await dbContext.AuthLockouts
            .SingleOrDefaultAsync(lockout => lockout.ClientId == clientId && lockout.IpAddress == ipAddress, cancellationToken);

        if (entry is null)
        {
            return;
        }

        entry.FailedAttempts = 0;
        entry.LockedUntilUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}