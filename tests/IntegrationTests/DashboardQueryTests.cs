using Core;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Web.Endpoints;
using Xunit;

namespace IntegrationTests;

public class DashboardQueryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset Now = TestClock.Now;

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(new FixedTimeProvider(Now)))
            .Options);

    private static (CBot cbot, LocalNode node, Instance[] instances) BuildFixture(UserId uid)
    {
        var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", []);
        var node = LocalNode.Create($"node-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);

        var tag = new DockerImageTag("latest");
        var symbol = new Symbol("EURUSD");
        var timeframe = new Timeframe("H1");

        var completed = ((StartingBacktestInstance)BacktestInstance.CreateStarting(
                uid, cbot.Id, node.Id, tag, symbol, timeframe, null))
            .ToRunning("c1", Now.AddMinutes(-30))
            .ToCompleted(Now.AddMinutes(-5));

        var stopped = ((StartingRunInstance)RunInstance.CreateStarting(
                uid, cbot.Id, node.Id, tag, symbol, timeframe))
            .ToRunning("c2", Now.AddMinutes(-40))
            .ToStopped(Now.AddMinutes(-10));

        var failed = ((StartingRunInstance)RunInstance.CreateStarting(
                uid, cbot.Id, node.Id, tag, symbol, timeframe))
            .ToRunning("c3", Now.AddMinutes(-20))
            .ToFailed("boom", Now.AddMinutes(-15));

        var running = ((StartingRunInstance)RunInstance.CreateStarting(
                uid, cbot.Id, node.Id, tag, symbol, timeframe))
            .ToRunning("c4", Now.AddMinutes(-10));

        return (cbot, node, [completed, stopped, failed, running]);
    }

    private async Task<UserId> SeedAsync()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();

        var user = OwnerUser.Create(new Email($"dash-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());
        var (cbot, node, instances) = BuildFixture(user.Id);

        setup.Users.Add(user);
        setup.CBots.Add(cbot);
        setup.Nodes.Add(node);
        setup.Instances.AddRange(instances);
        await setup.SaveChangesAsync();

        return user.Id;
    }

    [Fact]
    public async Task Overview_reports_status_kpis_activity_and_resources()
    {
        var uid = await SeedAsync();

        await using var read = CreateContext();
        var overview = await DashboardQuery.BuildAsync(read, uid, isAdmin: false, DashboardPeriod.Day, Now);

        overview.Status.Total.Should().Be(4);
        overview.Status.Completed.Should().Be(2, "one completed backtest + one stopped run");
        overview.Status.Failed.Should().Be(1);
        overview.Status.Running.Should().Be(1);

        overview.Kpis.ActiveNow.Should().Be(1);
        overview.Kpis.Completed.Should().Be(2);
        overview.Kpis.Failed.Should().Be(1);
        overview.Kpis.SuccessRate.Should().BeApproximately(2d / 3d, 0.0001);

        overview.TimeSeries.Sum(b => b.Completed).Should().Be(2);
        overview.TimeSeries.Sum(b => b.Failed).Should().Be(1);

        overview.Activity.Should().HaveCount(4);
        overview.Activity[0].At.Should().Be(Now.AddMinutes(-5), "the most recent event is first");
        overview.Activity.Should().OnlyContain(a => a.Symbol == "EURUSD");
        overview.Activity.Should().OnlyContain(a => a.CBot.StartsWith("bot-"));

        overview.Resources.CBots.Should().Be(1);
        overview.IsAdmin.Should().BeFalse();
        overview.Nodes.Should().BeNull("node health is admin-only");
    }

    [Fact]
    public async Task Overview_includes_node_health_for_admins()
    {
        var uid = await SeedAsync();

        await using var read = CreateContext();
        var overview = await DashboardQuery.BuildAsync(read, uid, isAdmin: true, DashboardPeriod.Day, Now);

        overview.IsAdmin.Should().BeTrue();
        overview.Nodes.Should().NotBeNull();
        overview.Nodes!.Total.Should().BeGreaterThanOrEqualTo(1);
        overview.Nodes.Active.Should().BeGreaterThanOrEqualTo(1);
        overview.Nodes.CapacityTotal.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task Overview_for_a_user_with_no_data_is_empty_not_null()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var user = OwnerUser.Create(new Email($"empty-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());
        setup.Users.Add(user);
        await setup.SaveChangesAsync();

        await using var read = CreateContext();
        var overview = await DashboardQuery.BuildAsync(read, user.Id, isAdmin: false, DashboardPeriod.Day, Now);

        overview.Status.Total.Should().Be(0);
        overview.Kpis.ActiveNow.Should().Be(0);
        overview.Kpis.SuccessRate.Should().Be(0);
        overview.Activity.Should().BeEmpty();
        overview.TimeSeries.Should().HaveCount(24);
    }
}
