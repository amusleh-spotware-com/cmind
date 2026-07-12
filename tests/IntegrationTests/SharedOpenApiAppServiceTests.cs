using Core;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Infrastructure.OpenApi;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

public class SharedOpenApiAppServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private static AppOptions OptionsWith(SharedOpenApiAppOptions? shared = null) =>
        new()
        {
            OpenApi = new OpenApiOptions
            {
                PublicBaseUrl = "https://app.test",
                SharedApp = shared ?? new SharedOpenApiAppOptions()
            }
        };

    private SharedOpenApiAppService CreateService(DataContext db, AppOptions options) =>
        new(db, new PassthroughSecretProtector(), new StaticOptionsMonitor<AppOptions>(options),
            NullLogger<SharedOpenApiAppService>.Instance);

    private async Task<DataContext> FreshAsync()
    {
        var db = CreateContext();
        await db.Database.MigrateAsync();
        await db.OpenApiAuthorizations.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.OpenApiApplications.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Users.IgnoreQueryFilters().ExecuteDeleteAsync();
        return db;
    }

    private static OwnerUser NewOwner() =>
        OwnerUser.Create(new Email($"owner-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());

    private static RegularUser NewUser() =>
        RegularUser.Create(new Email($"user-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());

    [Fact]
    public async Task SeedFromConfig_creates_one_shared_app_and_is_idempotent()
    {
        await using var db = await FreshAsync();
        db.Users.Add(NewOwner());
        await db.SaveChangesAsync();

        var options = OptionsWith(new SharedOpenApiAppOptions
        {
            Enabled = true, Name = "Broker App", ClientId = "shared-cid", ClientSecret = "shared-secret"
        });

        await CreateService(db, options).SeedFromConfigAsync(CancellationToken.None);
        await CreateService(db, options).SeedFromConfigAsync(CancellationToken.None); // idempotent

        await using var read = CreateContext();
        var shared = await read.OpenApiApplications.Where(a => a.IsShared).ToListAsync();
        shared.Should().ContainSingle();
        shared[0].ClientId.Should().Be("shared-cid");
        shared[0].RedirectUri.Should().Be("https://app.test/openapi/callback");
    }

    [Fact]
    public async Task SeedFromConfig_skips_when_public_base_url_missing()
    {
        await using var db = await FreshAsync();
        db.Users.Add(NewOwner());
        await db.SaveChangesAsync();

        var options = new AppOptions
        {
            OpenApi = new OpenApiOptions
            {
                PublicBaseUrl = null,
                SharedApp = new SharedOpenApiAppOptions { Enabled = true, ClientId = "cid", ClientSecret = "sec" }
            }
        };

        await CreateService(db, options).SeedFromConfigAsync(CancellationToken.None);

        await using var read = CreateContext();
        (await read.OpenApiApplications.AnyAsync(a => a.IsShared)).Should().BeFalse();
    }

    [Fact]
    public async Task Configuring_shared_removes_personal_apps_and_repoints_their_authorizations()
    {
        await using var db = await FreshAsync();
        var owner = NewOwner();
        var user = NewUser();
        var personal = OpenApiApplication.Create(user.Id, "mine", new OpenApiClientId("personal-cid"), [1],
            new OpenApiRedirectUri("https://app.test/openapi/callback"));
        var ctid = Math.Abs(Guid.NewGuid().GetHashCode()) + 1L;
        var authorization = OpenApiAuthorization.Create(user.Id, personal.Id, new CtidUserId(ctid),
            isLive: false, [9], [8], TestClock.Now.AddDays(30), OpenApiScope.Trade);
        db.Users.AddRange(owner, user);
        db.OpenApiApplications.Add(personal);
        db.OpenApiAuthorizations.Add(authorization);
        await db.SaveChangesAsync();

        var service = CreateService(db, OptionsWith());
        var shared = await service.SaveOwnerSharedAsync("Shared", new OpenApiClientId("shared-cid"), "shared-secret",
            new OpenApiRedirectUri("https://app.test/openapi/callback"), CancellationToken.None);

        await using var read = CreateContext();
        // Personal app removed; only the shared row remains.
        (await read.OpenApiApplications.AnyAsync(a => !a.IsShared)).Should().BeFalse();
        (await read.OpenApiApplications.CountAsync(a => a.IsShared)).Should().Be(1);
        // The account survives, re-pointed at the shared app (re-auth needed, not deleted).
        var reloaded = await read.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id);
        reloaded.ApplicationId.Should().Be(shared.Id);
    }

    [Fact]
    public async Task Remove_shared_reverts_to_per_user_mode()
    {
        await using var db = await FreshAsync();
        db.Users.Add(NewOwner());
        await db.SaveChangesAsync();
        var service = CreateService(db, OptionsWith());
        await service.SaveOwnerSharedAsync("Shared", new OpenApiClientId("shared-cid"), "shared-secret",
            new OpenApiRedirectUri("https://app.test/openapi/callback"), CancellationToken.None);

        (await service.RemoveSharedAsync(CancellationToken.None)).Should().BeTrue();

        await using var read = CreateContext();
        (await read.OpenApiApplications.AnyAsync(a => a.IsShared)).Should().BeFalse();
    }

    private sealed class PassthroughSecretProtector : ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, string purpose) => plaintext.ToArray();
        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, string purpose) => ciphertext.ToArray();
        public string ProtectString(string plaintext, string purpose) => plaintext;
        public string UnprotectString(string ciphertext, string purpose) => ciphertext;
    }
}
