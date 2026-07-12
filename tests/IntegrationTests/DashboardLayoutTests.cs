using Core;
using Core.Agent;
using Core.Dashboard;
using Core.Domain;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Web.Endpoints;
using Xunit;

namespace IntegrationTests;

public class DashboardLayoutTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
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

    private async Task<UserId> SeedUserAsync()
    {
        await using var setup = CreateContext();
        await setup.Database.MigrateAsync();
        var user = OwnerUser.Create(new Email($"layout-{Guid.NewGuid():N}@test.local"), "x", Guid.NewGuid().ToByteArray());
        setup.Users.Add(user);
        await setup.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Layout_persists_widget_order_and_visibility_across_a_reload()
    {
        var uid = await SeedUserAsync();

        await using (var write = CreateContext())
        {
            var board = UserDashboard.CreateDefault(uid);
            board.Apply(
            [
                new DashboardWidgetPreference(DashboardWidgets.Agents, true),
                new DashboardWidgetPreference(DashboardWidgets.Kpis, false)
            ]);
            write.UserDashboards.Add(board);
            await write.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var reloaded = await read.UserDashboards.AsNoTracking().SingleAsync(d => d.UserId == uid);

        var ordered = reloaded.Widgets.OrderBy(w => w.Order).ToList();
        ordered[0].Key.Should().Be(DashboardWidgets.Agents);
        ordered[1].Key.Should().Be(DashboardWidgets.Kpis);
        ordered.Single(w => w.Key == DashboardWidgets.Kpis).Visible.Should().BeFalse();
        // The full catalog is stored even though only two widgets were supplied.
        ordered.Select(w => w.Key).Should().BeEquivalentTo(DashboardWidgets.DefaultOrder);
    }

    [Fact]
    public async Task Overview_includes_backtests_copy_profiles_and_agents()
    {
        var uid = await SeedUserAsync();

        await using (var setup = CreateContext())
        {
            var cbot = CBot.Create(uid, $"bot-{Guid.NewGuid():N}", []);
            var node = LocalNode.Create($"node-{Guid.NewGuid():N}", "/var/app/data", 10, enabled: true);
            var completedBacktest = ((StartingBacktestInstance)BacktestInstance.CreateStarting(
                    uid, cbot.Id, node.Id, new DockerImageTag("latest"), new Symbol("EURUSD"), new Timeframe("H1"), null))
                .ToRunning("c1", Now.AddMinutes(-30))
                .ToCompleted(Now.AddMinutes(-5));

            var profile = CopyProfile.Create(uid, $"copy-{Guid.NewGuid():N}", TradingAccountId.New());
            var agent = TradingAgent.Create(uid, $"agent-{Guid.NewGuid():N}", AgentArchetype.Scalper, AgentTemperament.Balanced);

            setup.CBots.Add(cbot);
            setup.Nodes.Add(node);
            setup.Instances.Add(completedBacktest);
            setup.CopyProfiles.Add(profile);
            setup.TradingAgents.Add(agent);
            await setup.SaveChangesAsync();
        }

        await using var read = CreateContext();
        var overview = await DashboardQuery.BuildAsync(read, uid, isAdmin: false, DashboardPeriod.Day, Now);

        overview.Backtests.Completed.Should().Be(1);
        overview.Backtests.Running.Should().Be(0);
        overview.CopyProfiles.Should().ContainSingle().Which.Status.Should().Be("Draft");
        overview.Agents.Should().ContainSingle();
        overview.Agents[0].Archetype.Should().Be("Scalper");
        overview.Agents[0].IsRunning.Should().BeFalse();
    }
}
