using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using ConsoleApp1.Application.Contracts.Auth;
using ConsoleApp1.Domain.Entities;
using ConsoleApp1.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ConsoleApp1.Application.Services;

public class TokenService(
    HotelDbContext dbContext,
    SecurityAuditService securityAuditService,
    AuthLockoutService authLockoutService,
    JwtKeyProvider jwtKeyProvider,
    ILogger<TokenService> logger,
    IOptions<JwtOptions> jwtOptionsAccessor,
    IOptions<AuthClientOptions> authClientOptionsAccessor)
{
    private readonly JwtOptions _jwtOptions = jwtOptionsAccessor.Value;
    private readonly AuthClientOptions _authClientOptions = authClientOptionsAccessor.Value;
    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "reservations.write",
        "operations.read",
        "mcp.access",
        "mcp.tools.read",
        "mcp.availability.query",
        "mcp.housekeeping.read",
        "audit.read",
        "hotel.full"
    };

    public async Task<TokenResponse?> IssueAsync(
        TokenRequest request,
        AuthAuditContext context,
        CancellationToken cancellationToken = default)
    {
        var ip = NormalizeIp(context.IpAddress);
        if (await authLockoutService.IsLockedAsync(request.ClientId, ip, cancellationToken))
        {
            await securityAuditService.WriteAsync("auth.issue", false, request.ClientId, "locked", context, cancellationToken);
            return null;
        }

        if (!IsValidClient(request.ClientId, request.ClientSecret))
        {
            await authLockoutService.RegisterFailureAsync(request.ClientId, ip, cancellationToken);
            await securityAuditService.WriteAsync("auth.issue", false, request.ClientId, "invalid-client", context, cancellationToken);
            return null;
        }

        var scopes = NormalizeScopes(request.Scope);
        if (scopes.Count == 0)
        {
            await securityAuditService.WriteAsync("auth.issue", false, request.ClientId, "invalid-scope", context, cancellationToken);
            return null;
        }

        await authLockoutService.ResetAsync(request.ClientId, ip, cancellationToken);

        var now = DateTime.UtcNow;
        var accessToken = GenerateAccessToken(request.ClientId, scopes, now, out var accessTokenExpiresInSeconds);

        var refreshTokenValue = GenerateRefreshTokenValue();
        var familyId = Guid.NewGuid().ToString("N");
        var refreshToken = new RefreshToken
        {
            FamilyId = familyId,
            ClientId = request.ClientId,
            Scope = string.Join(' ', scopes),
            TokenHash = HashToken(refreshTokenValue),
            CreatedUtc = now,
            ExpiresUtc = now.AddDays(_authClientOptions.RefreshTokenDays)
        };

        await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditService.WriteAsync("auth.issue", true, request.ClientId, null, context, cancellationToken);

        logger.LogInformation("Access and refresh tokens issued for client {ClientId}", request.ClientId);

        return new TokenResponse(
            accessToken,
            "Bearer",
            accessTokenExpiresInSeconds,
            refreshTokenValue,
            (int)TimeSpan.FromDays(_authClientOptions.RefreshTokenDays).TotalSeconds);
    }

    public async Task<TokenResponse?> RefreshAsync(
        RefreshTokenRequest request,
        AuthAuditContext context,
        CancellationToken cancellationToken = default)
    {
        var ip = NormalizeIp(context.IpAddress);
        if (await authLockoutService.IsLockedAsync(request.ClientId, ip, cancellationToken))
        {
            await securityAuditService.WriteAsync("auth.refresh", false, request.ClientId, "locked", context, cancellationToken);
            return null;
        }

        if (!IsValidClient(request.ClientId, request.ClientSecret))
        {
            await authLockoutService.RegisterFailureAsync(request.ClientId, ip, cancellationToken);
            await securityAuditService.WriteAsync("auth.refresh", false, request.ClientId, "invalid-client", context, cancellationToken);
            return null;
        }

        await authLockoutService.ResetAsync(request.ClientId, ip, cancellationToken);

        var incomingHash = HashToken(request.RefreshToken);
        var now = DateTime.UtcNow;

        var currentToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == incomingHash && token.ClientId == request.ClientId, cancellationToken);

        if (currentToken is null || currentToken.ExpiresUtc <= now)
        {
            await securityAuditService.WriteAsync("auth.refresh", false, request.ClientId, "invalid-refresh-token", context, cancellationToken);
            return null;
        }

        if (currentToken.RevokedUtc.HasValue)
        {
            await RevokeFamilyAsync(currentToken.FamilyId, "refresh-token-reuse", cancellationToken);
            await securityAuditService.WriteAsync("auth.refresh", false, request.ClientId, "reused-refresh-token", context, cancellationToken);
            return null;
        }

        var scopes = NormalizeScopes(currentToken.Scope);
        if (scopes.Count == 0)
        {
            await securityAuditService.WriteAsync("auth.refresh", false, request.ClientId, "invalid-scope", context, cancellationToken);
            return null;
        }

        var newRefreshTokenValue = GenerateRefreshTokenValue();
        var newRefreshTokenHash = HashToken(newRefreshTokenValue);

        currentToken.RevokedUtc = now;
        currentToken.ReplacedByTokenHash = newRefreshTokenHash;
        currentToken.RevocationReason = "rotated";

        var newRefreshToken = new RefreshToken
        {
            FamilyId = currentToken.FamilyId,
            ClientId = currentToken.ClientId,
            Scope = currentToken.Scope,
            TokenHash = newRefreshTokenHash,
            CreatedUtc = now,
            ExpiresUtc = now.AddDays(_authClientOptions.RefreshTokenDays)
        };

        await dbContext.RefreshTokens.AddAsync(newRefreshToken, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await securityAuditService.WriteAsync("auth.refresh", true, request.ClientId, null, context, cancellationToken);

        var accessToken = GenerateAccessToken(currentToken.ClientId, scopes, now, out var accessTokenExpiresInSeconds);

        logger.LogInformation("Refresh token rotated for client {ClientId}", currentToken.ClientId);

        return new TokenResponse(
            accessToken,
            "Bearer",
            accessTokenExpiresInSeconds,
            newRefreshTokenValue,
            (int)TimeSpan.FromDays(_authClientOptions.RefreshTokenDays).TotalSeconds);
    }

    public async Task<bool> RevokeAsync(
        RevokeTokenRequest request,
        AuthAuditContext context,
        CancellationToken cancellationToken = default)
    {
        var ip = NormalizeIp(context.IpAddress);
        if (await authLockoutService.IsLockedAsync(request.ClientId, ip, cancellationToken))
        {
            await securityAuditService.WriteAsync("auth.revoke", false, request.ClientId, "locked", context, cancellationToken);
            return false;
        }

        if (!IsValidClient(request.ClientId, request.ClientSecret))
        {
            await authLockoutService.RegisterFailureAsync(request.ClientId, ip, cancellationToken);
            await securityAuditService.WriteAsync("auth.revoke", false, request.ClientId, "invalid-client", context, cancellationToken);
            return false;
        }

        await authLockoutService.ResetAsync(request.ClientId, ip, cancellationToken);

        var hash = HashToken(request.RefreshToken);
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(item => item.TokenHash == hash && item.ClientId == request.ClientId, cancellationToken);

        if (token is null)
        {
            await securityAuditService.WriteAsync("auth.revoke", false, request.ClientId, "refresh-token-not-found", context, cancellationToken);
            return false;
        }

        if (token.RevokedUtc.HasValue)
        {
            await securityAuditService.WriteAsync("auth.revoke", true, request.ClientId, "already-revoked", context, cancellationToken);
            return true;
        }

        await RevokeFamilyAsync(token.FamilyId, "manual-revoke", cancellationToken);
        await securityAuditService.WriteAsync("auth.revoke", true, request.ClientId, null, context, cancellationToken);

        logger.LogInformation("Refresh token revoked for client {ClientId}", request.ClientId);
        return true;
    }

    private bool IsValidClient(string clientId, string clientSecret)
    {
        return SecureEquals(clientId, _authClientOptions.ClientId) &&
               SecureEquals(clientSecret, _authClientOptions.ClientSecret);
    }

    private string GenerateAccessToken(
        string clientId,
        IReadOnlyCollection<string> scopes,
        DateTime now,
        out int expiresInSeconds)
    {
        var expires = now.AddMinutes(_jwtOptions.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var credentials = jwtKeyProvider.GetCurrentSigningCredentials(out var keyId);

        var header = new JwtHeader(credentials)
        {
            { "kid", keyId }
        };

        var payload = new JwtPayload(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims,
            notBefore: now,
            expires: expires);

        var token = new JwtSecurityToken(header, payload);

        expiresInSeconds = (int)(expires - now).TotalSeconds;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(bytes);
    }

    private static string HashToken(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }

    private static List<string> NormalizeScopes(string scopeInput)
    {
        return scopeInput
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(scope => AllowedScopes.Contains(scope))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        if (leftBytes.Length != rightBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private async Task RevokeFamilyAsync(string familyId, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var familyTokens = await dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in familyTokens)
        {
            token.RevokedUtc = now;
            token.RevocationReason = reason;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeIp(string? ip)
    {
        return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip.Trim();
    }
}