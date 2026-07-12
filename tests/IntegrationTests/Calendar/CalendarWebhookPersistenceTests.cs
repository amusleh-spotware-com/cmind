using Core;
using Core.Calendar;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Calendar;

public class CalendarWebhookPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    [Fact]
    public async Task Webhook_round_trips_its_filter_and_disable()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"wh-{Guid.NewGuid():N}@test.local"), "x",
            Guid.NewGuid().ToByteArray());
        var webhook = CalendarWebhook.Create(
            user.Id, "https://example.com/hook", [1, 2, 3, 4], ImpactLevel.High, "USD,EUR");

        await using (var write = CreateContext())
        {
            write.Users.Add(user);
            write.CalendarWebhooks.Add(webhook);
            await write.SaveChangesAsync();
        }

        await using (var read = CreateContext())
        {
            var loaded = await read.CalendarWebhooks.FirstAsync(w => w.Id == webhook.Id);
            loaded.Url.Should().Be("https://example.com/hook");
            loaded.MinImpact.Should().Be(ImpactLevel.High);
            loaded.Currencies.Should().Be("USD,EUR");
            loaded.EncryptedSecret.Should().Equal(1, 2, 3, 4);
            loaded.IsActive.Should().BeTrue();
        }

        await using (var disable = CreateContext())
        {
            var loaded = await disable.CalendarWebhooks.FirstAsync(w => w.Id == webhook.Id);
            loaded.Disable(TimeProvider.System.GetUtcNow());
            await disable.SaveChangesAsync();
        }

        await using var after = CreateContext();
        (await after.CalendarWebhooks.FirstAsync(w => w.Id == webhook.Id)).IsActive.Should().BeFalse();
    }
}
