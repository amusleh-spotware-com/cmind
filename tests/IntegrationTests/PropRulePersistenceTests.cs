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

        var user = new OwnerUser
        {
            Email = $"prop-{Guid.NewGuid():N}@test.local",
            NormalizedEmail = $"PROP-{Guid.NewGuid():N}@TEST.LOCAL",
            PasswordHash = "x",
            SecurityStamp = Guid.NewGuid().ToByteArray()
        };
        var ctid = new CTraderIdAccount
        {
            UserId = user.Id, User = user, Username = $"ct-{Guid.NewGuid():N}",
            EncryptedPassword = "pw"u8.ToArray()
        };
        var account = new TradingAccount { CTidId = ctid.Id, CTid = ctid, AccountNumber = 12345, Broker = "Test" };
        var rule = new PropRule
        {
            UserId = user.Id, TradingAccountId = account.Id, Name = "FTMO",
            MaxConcurrentLiveInstances = 2, AutoFlatten = true, MaxDrawdownPercent = 10
        };

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.CTids.Add(ctid);
            write.TradingAccounts.Add(account);
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
            dup.PropRules.Add(new PropRule
            {
                UserId = user.Id, TradingAccountId = account.Id, Name = "Duplicate"
            });
            var act = async () => await dup.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}
