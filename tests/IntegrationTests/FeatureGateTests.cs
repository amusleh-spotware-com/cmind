using Core;
using Core.Constants;
using Core.Features;
using Core.Options;
using FluentAssertions;
using Infrastructure.Features;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace IntegrationTests;

public class FeatureGateTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private DataContext CreateContext() =>
        new(new DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System))
            .Options);

    private FeatureGate CreateGate(DataContext db, AppOptions options) =>
        new(new StaticOptionsMonitor<AppOptions>(options), db, new MemoryCache(new MemoryCacheOptions()),
            TimeProvider.System);

    [Fact]
    public async Task Config_baseline_applies_when_no_override()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var gate = CreateGate(db, new AppOptions { Features = new FeaturesOptions { CopyTrading = false } });

        gate.IsEnabled(FeatureFlag.CopyTrading).Should().BeFalse();
        gate.IsEnabled(FeatureFlag.Ai).Should().BeTrue();
    }

    [Fact]
    public async Task Runtime_override_beats_config_and_persists()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var gate = CreateGate(db, new AppOptions());

        await gate.SetOverrideAsync(FeatureFlag.CopyTrading, false, CancellationToken.None);

        gate.IsEnabled(FeatureFlag.CopyTrading).Should().BeFalse();
        gate.Snapshot()[FeatureFlag.CopyTrading].Should().BeFalse();
        (await db.AppSettings.AnyAsync(s => s.Key == FeatureSettings.OverrideKey(FeatureFlag.CopyTrading)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Clearing_override_reverts_to_config_baseline()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var gate = CreateGate(db, new AppOptions());

        await gate.SetOverrideAsync(FeatureFlag.Alerts, false, CancellationToken.None);
        gate.IsEnabled(FeatureFlag.Alerts).Should().BeFalse();

        await gate.SetOverrideAsync(FeatureFlag.Alerts, null, CancellationToken.None);

        gate.IsEnabled(FeatureFlag.Alerts).Should().BeTrue();
        (await db.AppSettings.AnyAsync(s => s.Key == FeatureSettings.OverrideKey(FeatureFlag.Alerts)))
            .Should().BeFalse();
    }
}
