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

public class AiProviderUserScopeTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    // A fresh store per resolution so its (per-scope) active-provider cache never masks a live change.
    private AiProviderStore StoreFor(DataContext db, UserId? user) =>
        new(new StaticOptionsMonitor<AppOptions>(new AppOptions()), db,
            new MemoryCache(new MemoryCacheOptions()), new PassthroughSecretProtector(),
            new FakeCurrentUser(user), TimeProvider.System);

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.AiProviderCredentials.IgnoreQueryFilters().ExecuteDeleteAsync();
        return db;
    }

    private static UpsertAiProviderCommand Command(string key, bool activate) =>
        new(null, AiProviderKind.Anthropic, "https://api.anthropic.com/", "claude-opus-4-8", key, 8000, null, activate);

    [Fact]
    public async Task User_own_active_credential_wins_over_deployment_default()
    {
        await using var db = await FreshAsync();
        var alice = UserId.New();
        var bob = UserId.New();

        await StoreFor(db, null).UpsertAsync(Command("deployment-key", activate: true), CancellationToken.None);
        await StoreFor(db, alice).UpsertForUserAsync(alice, Command("alice-key", activate: true), CancellationToken.None);

        StoreFor(db, alice).Active!.ApiKey.Should().Be("alice-key");   // her own wins
        StoreFor(db, bob).Active!.ApiKey.Should().Be("deployment-key"); // no own -> default
        StoreFor(db, null).Active!.ApiKey.Should().Be("deployment-key"); // background -> default
    }

    [Fact]
    public async Task Activating_a_user_credential_does_not_deactivate_the_deployment_default()
    {
        await using var db = await FreshAsync();
        var alice = UserId.New();

        await StoreFor(db, null).UpsertAsync(Command("deployment-key", activate: true), CancellationToken.None);
        await StoreFor(db, alice).UpsertForUserAsync(alice, Command("alice-key", activate: true), CancellationToken.None);

        // Per-scope exclusivity: the deployment default stays active for everyone else.
        var deploymentActive = await db.AiProviderCredentials.CountAsync(c => c.OwnerUserId == null && c.IsActive);
        deploymentActive.Should().Be(1);
        StoreFor(db, UserId.New()).Active!.ApiKey.Should().Be("deployment-key");
    }

    [Fact]
    public async Task Removing_a_user_credential_falls_back_to_the_deployment_default()
    {
        await using var db = await FreshAsync();
        var alice = UserId.New();

        await StoreFor(db, null).UpsertAsync(Command("deployment-key", activate: true), CancellationToken.None);
        var id = await StoreFor(db, alice).UpsertForUserAsync(alice, Command("alice-key", activate: true), CancellationToken.None);
        StoreFor(db, alice).Active!.ApiKey.Should().Be("alice-key");

        await StoreFor(db, alice).RemoveForUserAsync(alice, id, CancellationToken.None);

        StoreFor(db, alice).Active!.ApiKey.Should().Be("deployment-key");
    }

    [Fact]
    public async Task A_user_cannot_touch_another_users_or_the_deployment_credential()
    {
        await using var db = await FreshAsync();
        var alice = UserId.New();
        var bob = UserId.New();
        var deploymentId = await StoreFor(db, null).UpsertAsync(Command("deployment-key", activate: true), CancellationToken.None);

        // Bob removing the deployment credential via his user scope is a no-op (scoped by OwnerUserId).
        await StoreFor(db, bob).RemoveForUserAsync(bob, deploymentId, CancellationToken.None);

        (await db.AiProviderCredentials.CountAsync(c => c.OwnerUserId == null)).Should().Be(1);
    }

    private sealed class FakeCurrentUser(UserId? user) : ICurrentUser
    {
        public UserId? UserId => user;
        public string? RoleName => null;
        public int? RoleRank => null;
        public string? Email => null;
        public bool IsInRole(string roleName) => false;
        public bool IsAtLeast(string roleName) => false;
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
