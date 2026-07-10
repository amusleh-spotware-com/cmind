using Core;
using Core.Domain;
using Core.PropFirm;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class PropFirmChallengePersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Challenge_round_trips_and_records_equity_and_soft_deletes()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"propfirm-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var challenge = PropFirmChallenge.Create(user.Id, TradingAccountId.New(), "FTMO 100k",
            new Money(100_000m),
            new ChallengeRules(new Percent(10), new Percent(5), new Percent(10),
                DrawdownMode.Static, new TradingDayRequirement(0), SingleStep: true));

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.PropFirmChallenges.Add(challenge);
            await write.SaveChangesAsync();
        }

        await using (var record = CreateContext())
        {
            var loaded = await record.PropFirmChallenges.FirstAsync(c => c.Id == challenge.Id);
            loaded.Phase.Should().Be(ChallengePhase.Evaluation);
            loaded.RecordEquity(new Money(110_000m), new DateTimeOffset(2026, 07, 10, 12, 0, 0, TimeSpan.Zero));
            await record.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var loaded = await read.PropFirmChallenges.FirstAsync(c => c.Id == challenge.Id);
            loaded.Status.Should().Be(ChallengeStatus.Passed);
            loaded.Phase.Should().Be(ChallengePhase.Funded);
            loaded.CurrentEquity.Should().Be(110_000m);
        }

        await using (var delete = CreateContext())
        {
            var loaded = await delete.PropFirmChallenges.FirstAsync(c => c.Id == challenge.Id);
            delete.PropFirmChallenges.Remove(loaded);
            await delete.SaveChangesAsync();
        }

        await using var after = CreateContext();
        (await after.PropFirmChallenges.AnyAsync(c => c.Id == challenge.Id)).Should().BeFalse();
    }
}
