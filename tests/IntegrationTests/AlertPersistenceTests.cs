using Core;
using Core.Domain;
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

        var user = OwnerUser.Create(new Email($"alert-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var rule = AlertRule.Create(user.Id, $"rule-{Guid.NewGuid():N}", new Symbol("EURUSD"),
            new EvaluationInterval(30));
        rule.Raise(AlertSeverity.Critical, "ECB surprise");

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.AlertRules.Add(rule);
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
