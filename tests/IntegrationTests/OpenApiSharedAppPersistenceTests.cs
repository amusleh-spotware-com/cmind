using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class OpenApiSharedAppPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private static OwnerUser NewOwner() =>
        OwnerUser.Create(new Email($"oapi-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());

    private static OpenApiApplication SharedFor(UserId owner) =>
        OpenApiApplication.CreateShared(owner, $"shared-{Guid.NewGuid():N}",
            new OpenApiClientId("client-shared"), [1, 2, 3], new OpenApiRedirectUri("https://app.test/openapi/callback"));

    // The class shares one Postgres container across its tests; reset the Open API app rows first so the
    // single-shared-app unique index is not tripped by a row a previous test left behind.
    private async Task ResetAsync()
    {
        await using var ctx = CreateContext();
        await ctx.Database.MigrateAsync();
        ctx.OpenApiAuthorizations.RemoveRange(await ctx.OpenApiAuthorizations.ToListAsync());
        ctx.OpenApiApplications.RemoveRange(await ctx.OpenApiApplications.ToListAsync());
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task Only_one_shared_application_is_allowed()
    {
        await ResetAsync();

        var owner1 = NewOwner();
        var owner2 = NewOwner();
        await using (var write = CreateContext())
        {
            write.Users.AddRange(owner1, owner2);
            write.OpenApiApplications.Add(SharedFor(owner1.Id));
            await write.SaveChangesAsync();
        }

        await using var second = CreateContext();
        second.OpenApiApplications.Add(SharedFor(owner2.Id));
        var act = () => second.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task GetByUser_excludes_shared_but_GetShared_finds_it()
    {
        await ResetAsync();

        var owner = NewOwner();
        var shared = SharedFor(owner.Id);
        await using (var write = CreateContext())
        {
            write.Users.Add(owner);
            write.OpenApiApplications.Add(shared);
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var repo = new OpenApiApplicationRepository(read);
        (await repo.GetByUserAsync(owner.Id, CancellationToken.None)).Should().BeNull();
        (await repo.GetSharedAsync(CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Reassigning_authorization_to_shared_app_persists()
    {
        await ResetAsync();

        var user = NewOwner();
        var deploymentOwner = NewOwner();
        var personal = OpenApiApplication.Create(user.Id, $"mine-{Guid.NewGuid():N}",
            new OpenApiClientId("client-personal"), [1], new OpenApiRedirectUri("https://app.test/openapi/callback"));
        var shared = SharedFor(deploymentOwner.Id);
        var ctid = Math.Abs(Guid.NewGuid().GetHashCode()) + 1L;
        var authorization = OpenApiAuthorization.Create(user.Id, personal.Id, new CtidUserId(ctid),
            isLive: false, [9, 9], [8, 8], TestClock.Now.AddDays(30), OpenApiScope.Trade);

        await using (var write = CreateContext())
        {
            write.Users.AddRange(user, deploymentOwner);
            write.OpenApiApplications.AddRange(personal, shared);
            write.OpenApiAuthorizations.Add(authorization);
            await write.SaveChangesAsync();
        }

        await using (var repoint = CreateContext())
        {
            var loaded = await repoint.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id);
            loaded.ReassignToApplication(shared.Id);
            await repoint.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var reloaded = await read.OpenApiAuthorizations.FirstAsync(a => a.Id == authorization.Id);
        reloaded.ApplicationId.Should().Be(shared.Id);
    }
}
