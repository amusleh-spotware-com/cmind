using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class PropRulePersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .Options);

    [Fact]
    public async Task PropRule_round_trips_and_enforces_unique_account()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"prop-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var ctid = CTraderIdAccount.Create(user.Id, $"ct-{Guid.NewGuid():N}", "pw"u8.ToArray());
        var account = ctid.AddTradingAccount(12345, "Test", isLive: false, label: null);
        var rule = PropRule.Create(user.Id, account.Id, "FTMO", 2, 0, 10, autoFlatten: true);

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.CTids.Add(ctid);
            write.PropRules.Add(rule);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var reloaded = await read.PropRules.FirstAsync(r => r.Id == rule.Id);
            reloaded.MaxConcurrentLiveInstances.Should().Be(2);
            reloaded.AutoFlatten.Should().BeTrue();
        }

        await using (var dup = CreateContext())
        {
            dup.PropRules.Add(PropRule.Create(user.Id, account.Id, "Duplicate", 3, 0, 0, autoFlatten: false));
            var act = async () => await dup.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}
