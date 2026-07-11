using Core;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class OpenApiTokenRefreshPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Expiry = new(2026, 07, 12, 12, 0, 0, TimeSpan.Zero);

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Refresh_failure_escalation_state_round_trips()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = RegularUser.Create(new Email($"tok-{Guid.NewGuid():N}@test.invalid"), "hash", [1], false);
        var app = OpenApiApplication.Create(user.Id, "app", new OpenApiClientId("client-1"), [9],
            new OpenApiRedirectUri("https://app.test/callback"));
        var auth = OpenApiAuthorization.Create(user.Id, app.Id, new CtidUserId(4242), true, [1], [2],
            Expiry, OpenApiScope.Trade);

        await using (var seed = CreateContext())
        {
            seed.Users.Add(user);
            seed.OpenApiApplications.Add(app);
            seed.OpenApiAuthorizations.Add(auth);
            await seed.SaveChangesAsync();
        }

        await using (var act = CreateContext())
        {
            var loaded = await act.OpenApiAuthorizations.FirstAsync(a => a.Id == auth.Id);
            loaded.MarkRefreshFailed("boom", Expiry - TimeSpan.FromHours(1), TimeSpan.FromHours(6));
            await act.SaveChangesAsync();
        }

        await using var verify = CreateContext();
        var reloaded = await verify.OpenApiAuthorizations.FirstAsync(a => a.Id == auth.Id);
        reloaded.ConsecutiveRefreshFailures.Should().Be(1);
        reloaded.RefreshCriticalAlerted.Should().BeTrue();
        reloaded.RefreshFailedAt.Should().NotBeNull();
    }
}
