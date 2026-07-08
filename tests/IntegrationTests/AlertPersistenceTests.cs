using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests;

public class AlertPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .Options);

    [Fact]
    public async Task Rule_and_event_round_trip_and_soft_delete()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = new OwnerUser
        {
            Email = $"alert-{Guid.NewGuid():N}@test.local",
            NormalizedEmail = $"ALERT-{Guid.NewGuid():N}@TEST.LOCAL",
            PasswordHash = "x",
            SecurityStamp = Guid.NewGuid().ToByteArray()
        };
        var rule = new AlertRule { UserId = user.Id, Name = $"rule-{Guid.NewGuid():N}", Symbol = "EURUSD", IntervalMinutes = 30 };

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.AlertRules.Add(rule);
            write.AlertEvents.Add(new AlertEvent
            {
                RuleId = rule.Id, UserId = user.Id, Severity = "critical", Message = "ECB surprise"
            });
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            (await read.AlertRules.FirstAsync(r => r.Id == rule.Id)).Symbol.Should().Be("EURUSD");
            var evt = await read.AlertEvents.FirstAsync(e => e.RuleId == rule.Id);
            evt.Severity.Should().Be("critical");
            evt.Acknowledged.Should().BeFalse();
        }

        await using (var delete = CreateContext())
        {
            delete.AlertRules.Remove(await delete.AlertRules.FirstAsync(r => r.Id == rule.Id));
            await delete.SaveChangesAsync();
        }

        await using var after = CreateContext();
        (await after.AlertRules.AnyAsync(r => r.Id == rule.Id)).Should().BeFalse();
    }
}
