using System.Text;
using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests.CopyLive;

// One-shot bootstrap: decrypt the existing Open API refresh/access tokens straight out of the
// running app's Postgres (the app-pg-data volume) using the exact same DataProtection setup, and
// cache them to secrets/openapi-tokens.local.json (gitignored). After this, the live copy tests
// refresh the access token from the cached refresh token — no browser, no prompts, on every rerun.
//
// Run once with the volume DB reachable:
//   CMIND_VOLUME_CONN="Host=127.0.0.1;Port=5544;Database=appdb;Username=postgres;Password=appdev" \
//     dotnet test tests/IntegrationTests --filter FullyQualifiedName~LiveTokenBootstrapTests
public sealed class LiveTokenBootstrapTests
{
    private const string ApplicationName = "app";

    [Fact]
    public async Task Extract_tokens_from_volume_and_cache()
    {
        var conn = Environment.GetEnvironmentVariable("CMIND_VOLUME_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return; // not a bootstrap run — skip.

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<DataContext>(o => o.UseNpgsql(conn));
        services.AddDataProtection().PersistKeysToDbContext<DataContext>().SetApplicationName(ApplicationName);
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        await using var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<DataContext>();
        var protector = provider.GetRequiredService<ISecretProtector>();

        var auth = await db.OpenApiAuthorizations.OrderByDescending(a => a.CreatedAt).FirstAsync();
        var refresh = Encoding.UTF8.GetString(
            protector.Unprotect(auth.EncryptedRefreshToken, EncryptionPurposes.OpenApiRefreshToken));
        var access = Encoding.UTF8.GetString(
            protector.Unprotect(auth.EncryptedAccessToken, EncryptionPurposes.OpenApiAccessToken));

        var rows = await db.TradingAccounts.Where(t => t.OpenApiAuthorizationId == auth.Id).ToListAsync();
        var accounts = rows
            .Where(t => t.CtidTraderAccountId != null)
            .Select(t => new LiveCopySecrets.CachedAccount(t.CtidTraderAccountId!.Value, t.AccountNumber, t.IsLive))
            .ToList();

        Assert.NotEmpty(accounts);
        LiveCopySecrets.SaveTokens(new LiveCopySecrets.TokenCache(refresh, access, auth.IsLive, accounts));
    }
}
