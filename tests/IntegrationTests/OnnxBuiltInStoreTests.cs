using Core;
using Core.Ai;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace IntegrationTests;

public class OnnxBuiltInStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private static AiProviderStore CreateStore(DataContext db, AppOptions options) =>
        new(new StaticOptionsMonitor<AppOptions>(options), db,
            new MemoryCache(new MemoryCacheOptions()), new PassthroughSecretProtector(), new NullCurrentUser(),
            TimeProvider.System);

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.AiProviderCredentials.IgnoreQueryFilters().ExecuteDeleteAsync();
        return db;
    }

    [Fact]
    public async Task Built_in_is_seeded_and_active_by_default()
    {
        await using var db = await FreshAsync();
        await CreateStore(db, new AppOptions()).SeedFromConfigAsync(CancellationToken.None);

        var store = CreateStore(db, new AppOptions());
        store.HasActive.Should().BeTrue();
        store.Active!.Kind.Should().Be(AiProviderKind.BuiltInOnnx);
        store.Active.ApiKey.Should().BeNull();
    }

    [Fact]
    public async Task Enabling_built_in_again_reuses_the_seeded_row_without_conflict()
    {
        await using var db = await FreshAsync();
        await CreateStore(db, new AppOptions()).SeedFromConfigAsync(CancellationToken.None);
        (await db.AiProviderCredentials.CountAsync()).Should().Be(1);

        // Enabling the built-in ONNX provider from settings must reuse the already-seeded row — not insert
        // a duplicate, and not 409 on the partial-unique active index (the reported "same key" conflict).
        var act = async () => await CreateStore(db, new AppOptions()).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.BuiltInOnnx, "https://builtin.local/", "built-in-onnx", null, 1024, null, Activate: true),
            CancellationToken.None);
        await act.Should().NotThrowAsync();

        (await db.AiProviderCredentials.CountAsync()).Should().Be(1);
        (await db.AiProviderCredentials.CountAsync(c => c.IsActive)).Should().Be(1);
        CreateStore(db, new AppOptions()).Active!.Kind.Should().Be(AiProviderKind.BuiltInOnnx);
    }

    [Fact]
    public async Task Activating_a_new_provider_deactivates_the_active_one_without_conflict()
    {
        await using var db = await FreshAsync();
        await CreateStore(db, new AppOptions()).SeedFromConfigAsync(CancellationToken.None);

        // Adding + activating a second provider while the seeded built-in is active must flip the active
        // flag atomically (deactivate old, activate new) without tripping the partial-unique active index.
        var act = async () => await CreateStore(db, new AppOptions()).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", "gpt-4o", "k", 4000, null, Activate: true),
            CancellationToken.None);
        await act.Should().NotThrowAsync();

        (await db.AiProviderCredentials.CountAsync()).Should().Be(2);
        (await db.AiProviderCredentials.CountAsync(c => c.IsActive)).Should().Be(1);
        CreateStore(db, new AppOptions()).Active!.Kind.Should().Be(AiProviderKind.OpenAiCompatible);
    }

    [Fact]
    public async Task White_label_can_remove_the_built_in()
    {
        await using var db = await FreshAsync();
        var options = new AppOptions { Branding = new BrandingOptions { AllowBuiltInAi = false } };
        await CreateStore(db, options).SeedFromConfigAsync(CancellationToken.None);

        CreateStore(db, options).HasActive.Should().BeFalse();
    }

    [Fact]
    public async Task Built_in_not_seeded_when_disabled_in_config()
    {
        await using var db = await FreshAsync();
        var options = new AppOptions { Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { Enabled = false } } };
        await CreateStore(db, options).SeedFromConfigAsync(CancellationToken.None);

        CreateStore(db, options).HasActive.Should().BeFalse();
    }

    [Fact]
    public async Task Upsert_rejects_a_kind_the_white_label_forbids()
    {
        await using var db = await FreshAsync();
        var options = new AppOptions { Branding = new BrandingOptions { AllowedAiProviderKinds = ["Anthropic"] } };
        var act = async () => await CreateStore(db, options).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.Gemini, "https://generativelanguage.googleapis.com/", "gemini-2.0-flash", "k",
            8000, null, Activate: true), CancellationToken.None);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(Core.Constants.DomainErrors.AiProviderKindNotAllowed);
    }

    [Fact]
    public async Task Upsert_rejects_a_local_provider_when_forbidden()
    {
        await using var db = await FreshAsync();
        var options = new AppOptions { Branding = new BrandingOptions { AllowLocalProviders = false } };
        var act = async () => await CreateStore(db, options).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "http://localhost:11434/v1/", "llama3.1:8b", null,
            4000, null, Activate: true), CancellationToken.None);
        (await act.Should().ThrowAsync<DomainException>()).Which.Code.Should().Be(Core.Constants.DomainErrors.AiLocalProviderNotAllowed);
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
