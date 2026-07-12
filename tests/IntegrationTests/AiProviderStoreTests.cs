using Core;
using Core.Ai;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace IntegrationTests;

public class AiProviderStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private static AiProviderStore CreateStore(DataContext db, AppOptions? options = null) =>
        new(new StaticOptionsMonitor<AppOptions>(options ?? new AppOptions()), db,
            new MemoryCache(new MemoryCacheOptions()), new PassthroughSecretProtector(), TimeProvider.System);

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.AiProviderCredentials.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.AppSettings.Where(s => s.Key == Core.Constants.AiConstants.ApiKeySettingKey).ExecuteDeleteAsync();
        return db;
    }

    [Fact]
    public async Task Disabled_when_no_credentials_and_no_config()
    {
        await using var db = await FreshAsync();
        var store = CreateStore(db);
        store.HasActive.Should().BeFalse();
        store.Active.Should().BeNull();
    }

    [Fact]
    public async Task Legacy_config_key_synthesizes_default_anthropic_active()
    {
        await using var db = await FreshAsync();
        var store = CreateStore(db, new AppOptions { Ai = new AiOptions { ApiKey = "sk-legacy" } });

        store.HasActive.Should().BeTrue();
        store.Active!.Kind.Should().Be(AiProviderKind.Anthropic);
        store.Active.ApiKey.Should().Be("sk-legacy");
    }

    [Fact]
    public async Task Upsert_creates_activates_and_round_trips_encrypted_key()
    {
        await using var db = await FreshAsync();
        var id = await CreateStore(db).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", "gpt-4o", "sk-secret",
            8000, null, Activate: true), CancellationToken.None);

        // Fresh store (cold cache) resolves the active provider with the key decrypted.
        var store = CreateStore(db);
        store.HasActive.Should().BeTrue();
        store.Active!.Kind.Should().Be(AiProviderKind.OpenAiCompatible);
        store.Active.Model.Should().Be("gpt-4o");
        store.Active.ApiKey.Should().Be("sk-secret");

        var list = await store.ListAsync(CancellationToken.None);
        list.Should().ContainSingle(p => p.Id == id && p.IsActive && p.HasKey);
    }

    [Fact]
    public async Task Keyless_local_provider_is_active_with_null_key()
    {
        await using var db = await FreshAsync();
        await CreateStore(db).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "http://localhost:11434/v1/", "llama3.1:8b", null,
            4000, null, Activate: true), CancellationToken.None);

        var store = CreateStore(db);
        store.HasActive.Should().BeTrue();
        store.Active!.ApiKey.Should().BeNull();
        (await store.ListAsync(CancellationToken.None)).Single().HasKey.Should().BeFalse();
    }

    [Fact]
    public async Task Activating_one_deactivates_the_previously_active()
    {
        await using var db = await FreshAsync();
        var store = CreateStore(db);
        var first = await store.UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.Anthropic, "https://api.anthropic.com/", "claude-opus-4-8", "k1", 8000, null, Activate: true),
            CancellationToken.None);
        var second = await store.UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.Gemini, "https://generativelanguage.googleapis.com/", "gemini-2.0-flash", "k2", 8000, null, Activate: true),
            CancellationToken.None);

        var verify = CreateStore(db);
        var list = await verify.ListAsync(CancellationToken.None);
        list.Count(p => p.IsActive).Should().Be(1);
        list.Single(p => p.IsActive).Id.Should().Be(second);
        verify.Active!.Kind.Should().Be(AiProviderKind.Gemini);

        // And explicit re-activation of the first flips exclusivity back.
        await verify.ActivateAsync(first, CancellationToken.None);
        (await CreateStore(db).ListAsync(CancellationToken.None)).Single(p => p.IsActive).Id.Should().Be(first);
    }

    [Fact]
    public async Task Remove_deletes_and_clears_active()
    {
        await using var db = await FreshAsync();
        var store = CreateStore(db);
        var id = await store.UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", "gpt-4o", "k", 8000, null, Activate: true),
            CancellationToken.None);

        await store.RemoveAsync(id, CancellationToken.None);

        var verify = CreateStore(db);
        verify.HasActive.Should().BeFalse();
        (await verify.ListAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task SeedFromConfig_imports_configured_providers_when_store_empty()
    {
        await using var db = await FreshAsync();
        var options = new AppOptions
        {
            Ai = new AiOptions
            {
                ActiveProvider = AiProviderKind.OpenAiCompatible,
                Providers =
                [
                    new AiProviderOptions { Kind = AiProviderKind.OpenAiCompatible, BaseUrl = "http://localhost:11434/v1/", Model = "qwen2.5:0.5b" }
                ]
            }
        };

        await CreateStore(db, options).SeedFromConfigAsync(CancellationToken.None);

        var store = CreateStore(db, options);
        store.HasActive.Should().BeTrue();
        store.Active!.Kind.Should().Be(AiProviderKind.OpenAiCompatible);
        store.Active.Model.Should().Be("qwen2.5:0.5b");
        store.Active.ApiKey.Should().BeNull();

        // Idempotent: a second seed does not duplicate.
        await CreateStore(db, options).SeedFromConfigAsync(CancellationToken.None);
        (await CreateStore(db).ListAsync(CancellationToken.None)).Should().ContainSingle();
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
