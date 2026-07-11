using Core;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace IntegrationTests;

public class AiKeyStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private static AiKeyStore CreateStore(DataContext db, AppOptions options) =>
        new(new StaticOptionsMonitor<AppOptions>(options), db, new MemoryCache(new MemoryCacheOptions()),
            new PassthroughSecretProtector(), TimeProvider.System);

    // The Postgres container is shared across this class's tests, so start each from a clean slate.
    private static async Task ResetAsync(DataContext db)
    {
        await db.Database.MigrateAsync();
        await CreateStore(db, new AppOptions()).ClearKeyAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Disabled_when_no_stored_or_config_key()
    {
        await using var db = CreateContext();
        await ResetAsync(db);
        var store = CreateStore(db, new AppOptions());

        store.HasKey.Should().BeFalse();
        store.HasStoredKey.Should().BeFalse();
        store.CurrentKey.Should().BeNull();
    }

    [Fact]
    public async Task Config_key_enables_without_stored_key()
    {
        await using var db = CreateContext();
        await ResetAsync(db);
        var store = CreateStore(db, new AppOptions { Ai = new AiOptions { ApiKey = "sk-config" } });

        store.HasKey.Should().BeTrue();
        store.HasStoredKey.Should().BeFalse();
        store.CurrentKey.Should().Be("sk-config");
    }

    [Fact]
    public async Task Stored_key_overrides_config_and_persists_encrypted()
    {
        await using var db = CreateContext();
        await ResetAsync(db);
        var options = new AppOptions { Ai = new AiOptions { ApiKey = "sk-config" } };

        await CreateStore(db, options).SetKeyAsync("sk-stored", CancellationToken.None);

        // Fresh store (cold cache) reads the persisted key back.
        var store = CreateStore(db, options);
        store.HasStoredKey.Should().BeTrue();
        store.CurrentKey.Should().Be("sk-stored");

        var raw = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == Core.Constants.AiConstants.ApiKeySettingKey)
            .Select(s => s.Value).FirstAsync();
        raw.Should().NotBe("sk-stored"); // stored encrypted (base64), not plaintext
    }

    [Fact]
    public async Task Clearing_stored_key_reverts_to_config()
    {
        await using var db = CreateContext();
        await ResetAsync(db);
        var options = new AppOptions { Ai = new AiOptions { ApiKey = "sk-config" } };

        await CreateStore(db, options).SetKeyAsync("sk-stored", CancellationToken.None);
        await CreateStore(db, options).ClearKeyAsync(CancellationToken.None);

        var store = CreateStore(db, options);
        store.HasStoredKey.Should().BeFalse();
        store.CurrentKey.Should().Be("sk-config");
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
