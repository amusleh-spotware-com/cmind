using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Core;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp.Auth;

public sealed class McpKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly CtwDbContext _db;

    public McpKeyAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        UrlEncoder encoder, CtwDbContext db) : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var auth)) return AuthenticateResult.NoResult();
        var value = auth.ToString();
        if (!value.StartsWith("Bearer ctw_mcp_", StringComparison.Ordinal))
            return AuthenticateResult.Fail("invalid scheme");

        var token = value["Bearer ".Length..];
        var prefix = token[..16];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var key = await _db.McpApiKeys.Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix && k.RevokedAt == null);
        if (key is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(key.KeyHash), Encoding.UTF8.GetBytes(hash)))
            return AuthenticateResult.Fail("invalid key");

        key.LastUsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, key.UserId.ToString()),
            new Claim(ClaimTypes.Email, key.User.Email),
            new Claim(ClaimTypes.Role, key.User.Role.ToString())
        };
        var identity = new ClaimsIdentity(claims, "McpKey");
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "McpKey"));
    }
}
