using Core;
using Core.CopyTrading;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nodes.CopyTrading;
using Testcontainers.PostgreSql;
using Xunit;

namespace IntegrationTests.CopyLive;

// 2b notification routing against a real Postgres: the drainer resolves each notification's owner from its
// profile and persists it to the per-owner CopyNotification feed; a notification for an unknown profile
// (owner not resolvable) is dropped rather than orphaned.
public sealed class CopyNotificationDrainerTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17").Build();
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();
        await using var db = NewContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private DataContext NewContext()
        => new(new DbContextOptionsBuilder<DataContext>().UseNpgsql(_connectionString)
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)).Options);

    [Fact]
    public async Task Drainer_resolves_the_owner_and_persists_notifications()
    {
        UserId ownerId;
        CopyProfileId profileId;
        await using (var seed = NewContext())
        {
            var user = RegularUser.Create(new Email($"u{Guid.NewGuid():N}@example.com"), "hash", new byte[] { 1, 2, 3 });
            seed.Add(user);
            var profile = CopyProfile.Create(user.Id, $"p-{Guid.NewGuid():N}", TradingAccountId.New());
            seed.Add(profile);
            await seed.SaveChangesAsync();
            ownerId = user.Id;
            profileId = profile.Id;
        }

        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(o => o
            .UseNpgsql(_connectionString)
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)));
        await using var provider = services.BuildServiceProvider();

        var sink = new ChannelCopyNotificationSink();
        var drainer = new CopyNotificationDrainer(sink, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CopyNotificationDrainer>.Instance, TimeProvider.System);

        sink.Notify(new CopyNotificationRecord(profileId, 200, CopyNotificationKind.DestinationTripped,
            CopyNotificationSeverity.Warning, "tripped", TestClock.Now));
        sink.Notify(new CopyNotificationRecord(profileId, null, CopyNotificationKind.FlattenAll,
            CopyNotificationSeverity.Critical, "flattened", TestClock.Now));
        // A notification for a profile that does not exist — the owner can't be resolved, so it is dropped.
        sink.Notify(new CopyNotificationRecord(CopyProfileId.New(), 999, CopyNotificationKind.PropRuleBreached,
            CopyNotificationSeverity.Critical, "orphan", TestClock.Now));

        await drainer.DrainOnceAsync(default);

        await using var db = NewContext();
        var rows = await db.CopyNotifications.Where(x => x.UserId == ownerId).ToListAsync();
        rows.Should().HaveCount(2, "both notifications for the known profile are persisted to its owner");
        rows.Should().Contain(x => x.Kind == CopyNotificationKind.DestinationTripped && x.DestinationCtidTraderAccountId == 200);
        rows.Should().Contain(x => x.Kind == CopyNotificationKind.FlattenAll && x.DestinationCtidTraderAccountId == null);
        (await db.CopyNotifications.CountAsync()).Should().Be(2, "the notification for the unknown profile is dropped");
    }
}
