using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class OpenApiAuthorizationPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Application_and_authorization_round_trip_and_soft_delete()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"oapi-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var application = OpenApiApplication.Create(user.Id, $"app-{Guid.NewGuid():N}",
            new OpenApiClientId("client-123"), [1, 2, 3], new OpenApiRedirectUri("https://app.test/callback"));
        var ctid = Math.Abs(Guid.NewGuid().GetHashCode()) + 1L;
        var authorization = OpenApiAuthorization.Create(user.Id, application.Id, new CtidUserId(ctid),
            isLive: true, [9, 9], [8, 8], TestClock.Now.AddDays(30), OpenApiScope.Trade);

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.OpenApiApplications.Add(application);
            write.OpenApiAuthorizations.Add(authorization);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var loaded = await read.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id);
            loaded.CtidUserId.Should().Be(ctid);
            loaded.Scope.Should().Be(OpenApiScope.Trade);
            loaded.IsLive.Should().BeTrue();
            loaded.EncryptedAccessToken.Should().Equal(new byte[] { 9, 9 });
        }

        await using (var delete = CreateContext())
        {
            delete.OpenApiAuthorizations.Remove(
                await delete.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id));
            await delete.SaveChangesAsync();
        }

        await using var after = CreateContext();
        (await after.OpenApiAuthorizations.AnyAsync(a => a.Id == authorization.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Token_version_increments_and_persists_on_refresh()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"oapi-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());
        var application = OpenApiApplication.Create(user.Id, $"app-{Guid.NewGuid():N}",
            new OpenApiClientId("client-123"), [1, 2, 3], new OpenApiRedirectUri("https://app.test/callback"));
        var ctid = Math.Abs(Guid.NewGuid().GetHashCode()) + 1L;
        var authorization = OpenApiAuthorization.Create(user.Id, application.Id, new CtidUserId(ctid),
            isLive: false, [9, 9], [8, 8], TestClock.Now.AddDays(30), OpenApiScope.Trade);
        var initialVersion = authorization.TokenVersion;

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.OpenApiApplications.Add(application);
            write.OpenApiAuthorizations.Add(authorization);
            await write.SaveChangesAsync();
        }

        // A cID's single valid access token rotates (refresh, or re-auth when another account is linked
        // on the same cID). The version marker must advance so a running copy host detects the change.
        await using (var rotate = CreateContext())
        {
            var loaded = await rotate.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id);
            loaded.Refresh([7, 7], [6, 6], TestClock.Now.AddDays(60), TestClock.Now);
            await rotate.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var reloaded = await read.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id);
        reloaded.TokenVersion.Should().Be(initialVersion + 1);
        reloaded.EncryptedAccessToken.Should().Equal(new byte[] { 7, 7 });
    }
}
