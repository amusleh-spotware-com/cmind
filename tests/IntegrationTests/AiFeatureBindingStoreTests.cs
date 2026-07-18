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

public class AiFeatureBindingStoreTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private static AiProviderStore CreateStore(DataContext db) =>
        new(new StaticOptionsMonitor<AppOptions>(new AppOptions()), db,
            new MemoryCache(new MemoryCacheOptions()), new PassthroughSecretProtector(), new NullCurrentUser(),
            TimeProvider.System);

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.AiFeatureBindings.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.AiProviderCredentials.IgnoreQueryFilters().ExecuteDeleteAsync();
        return db;
    }

    [Fact]
    public async Task ResolveFor_prefers_binding_then_falls_back_to_active()
    {
        await using var db = await FreshAsync();
        await CreateStore(db).SeedFromConfigAsync(CancellationToken.None); // built-in seeded + active
        var openAiId = await CreateStore(db).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", "gpt-4o", "k", 4000, null,
            Activate: false), CancellationToken.None);

        // No binding yet — every feature falls back to the scope's active provider (built-in).
        CreateStore(db).ResolveFor(AiFeature.GenerateCBot, null)!.Kind.Should().Be(AiProviderKind.BuiltInOnnx);

        // Bind ONE feature to the OpenAI-compatible provider — only that feature routes there.
        await CreateStore(db).SetBindingAsync(
            null, AiFeature.GenerateCBot, AiProviderCredentialId.From(openAiId), CancellationToken.None);

        var store = CreateStore(db);
        store.ResolveFor(AiFeature.GenerateCBot, null)!.Kind.Should().Be(AiProviderKind.OpenAiCompatible);
        store.ResolveFor(AiFeature.ReviewCBot, null)!.Kind.Should().Be(AiProviderKind.BuiltInOnnx);

        // An explicit credential overrides bindings entirely (the async-task "run on this model" path).
        store.ResolveFor(AiFeature.ReviewCBot, AiProviderCredentialId.From(openAiId))!
            .Kind.Should().Be(AiProviderKind.OpenAiCompatible);
    }

    [Fact]
    public async Task SetBinding_is_idempotent_and_retargets_in_place()
    {
        await using var db = await FreshAsync();
        await CreateStore(db).SeedFromConfigAsync(CancellationToken.None);
        var a = await CreateStore(db).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", "gpt-4o", "k", 4000, null, false),
            CancellationToken.None);
        var b = await CreateStore(db).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.mistral.ai/v1/", "mistral-large", "k", 4000, null, false),
            CancellationToken.None);

        await CreateStore(db).SetBindingAsync(null, AiFeature.FixCBot, AiProviderCredentialId.From(a), CancellationToken.None);
        await CreateStore(db).SetBindingAsync(null, AiFeature.FixCBot, AiProviderCredentialId.From(b), CancellationToken.None);

        // One binding row per (scope, feature) — the second Set retargets rather than inserting a duplicate.
        (await db.AiFeatureBindings.CountAsync(x => x.Feature == AiFeature.FixCBot)).Should().Be(1);
        var bindings = await CreateStore(db).ListBindingsAsync(null, CancellationToken.None);
        bindings.Should().ContainSingle(x => x.Feature == AiFeature.FixCBot).Which.CredentialId.Should().Be(b);
    }

    [Fact]
    public async Task ClearBinding_reverts_the_feature_to_the_active_provider()
    {
        await using var db = await FreshAsync();
        await CreateStore(db).SeedFromConfigAsync(CancellationToken.None);
        var openAiId = await CreateStore(db).UpsertAsync(new UpsertAiProviderCommand(
            null, AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", "gpt-4o", "k", 4000, null, false),
            CancellationToken.None);
        await CreateStore(db).SetBindingAsync(
            null, AiFeature.GenerateCBot, AiProviderCredentialId.From(openAiId), CancellationToken.None);

        await CreateStore(db).ClearBindingAsync(null, AiFeature.GenerateCBot, CancellationToken.None);

        CreateStore(db).ResolveFor(AiFeature.GenerateCBot, null)!.Kind.Should().Be(AiProviderKind.BuiltInOnnx);
        (await CreateStore(db).ListBindingsAsync(null, CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task SetBinding_rejects_a_credential_the_scope_cannot_use()
    {
        await using var db = await FreshAsync();
        await CreateStore(db).SeedFromConfigAsync(CancellationToken.None);

        var act = async () => await CreateStore(db).SetBindingAsync(
            null, AiFeature.GenerateCBot, AiProviderCredentialId.New(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
