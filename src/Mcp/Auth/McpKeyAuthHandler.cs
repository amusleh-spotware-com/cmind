using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Core.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Mcp.Auth;

public sealed class McpKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    CtwDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string AuthorizationHeader = "Authorization";
    private const int KeyPrefixLength = 16;
    private static readonly string BearerSpace = AuthSchemes.Bearer + " ";
    private static readonly string BearerTokenStart = BearerSpace + AuthSchemes.McpTokenPrefix;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthorizationHeader, out var auth))
            return AuthenticateResult.NoResult();
        var value = auth.ToString();
        if (!value.StartsWith(BearerTokenStart, StringComparison.Ordinal))
            return AuthenticateResult.Fail("invalid scheme");

        var token = value[BearerSpace.Length..];
        var prefix = token[..KeyPrefixLength];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var key = await db.McpApiKeys.Include(k => k.User)
            .FirstOrDefaultAsync(k => k.KeyPrefix == prefix && k.RevokedAt == null);
        if (key is null || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(key.KeyHash), Encoding.UTF8.GetBytes(hash)))
            return AuthenticateResult.Fail("invalid key");

        key.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, key.UserId.Value.ToString()),
            new Claim(ClaimTypes.Email, key.User.Email),
            new Claim(ClaimTypes.Role, key.User.RoleName)
        };
        var identity = new ClaimsIdentity(claims, AuthSchemes.McpKey);
        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemes.McpKey));
    }
}
