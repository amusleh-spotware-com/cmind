using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core;
using Core.Calendar;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Web.Calendar;

/// <summary>
/// Issues and validates the short-lived HS256 JWTs the Calendar API hands to registered clients — the same
/// symmetric-key pattern the node agents use. The signing key is generated once and persisted as an app
/// setting so it survives restarts and is shared across instances; it is cached in memory for hot validation.
/// Tokens carry the client id as <c>sub</c>, a space-separated <c>scope</c> claim, and a tight expiry.
/// </summary>
public sealed class CalendarJwt(
    DataContext db,
    IMemoryCache cache,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider)
{
    public const string Issuer = "cmind-calendar";
    public const string Audience = "calendar-api";
    public const string ScopeClaim = "scope";

    private const string SigningKeySetting = "calendar.jwt.signing_key";
    private const string CacheKey = "calendar:jwt:key";

    public async Task<(string Token, DateTimeOffset ExpiresAt)> IssueAsync(CalendarApiClient client, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + options.CurrentValue.Calendar.ApiTokenLifetime;
        var key = await GetSigningKeyAsync(ct);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, client.Id.Value.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ScopeClaim, string.Join(' ', client.Scopes))
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>Validates a bearer token and returns its principal, or <c>null</c> when it is invalid/expired.</summary>
    public async Task<ClaimsPrincipal?> ValidateAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = await GetSigningKeyAsync(ct);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> ScopesOf(ClaimsPrincipal principal) =>
        principal.FindFirst(ScopeClaim)?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

    private async Task<byte[]> GetSigningKeyAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(CacheKey, out byte[]? cached) && cached is not null) return cached;

        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == SigningKeySetting, ct);
        byte[] key;
        if (setting is null)
        {
            key = RandomNumberGenerator.GetBytes(32);
            db.AppSettings.Add(AppSetting.Create(SigningKeySetting, Convert.ToBase64String(key), timeProvider.GetUtcNow()));
            await db.SaveChangesAsync(ct);
        }
        else
        {
            key = Convert.FromBase64String(setting.Value);
        }

        cache.Set(CacheKey, key, TimeSpan.FromMinutes(10));
        return key;
    }
}
