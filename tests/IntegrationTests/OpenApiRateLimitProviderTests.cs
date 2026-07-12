using Core.Options;
using CTraderOpenApi.RateLimiting;
using FluentAssertions;
using Infrastructure.OpenApi;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class OpenApiRateLimitProviderTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private ServiceProvider BuildScope()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DataContext>(o => o
            .UseNpgsql(fixture.Container.GetConnectionString())
            .AddInterceptors(new AuditStampingInterceptor(TimeProvider.System)));
        return services.BuildServiceProvider();
    }

    private static OpenApiRateLimitProvider Provider(ServiceProvider sp, AppOptions options) =>
        new(sp.GetRequiredService<IServiceScopeFactory>(), new StaticOptionsMonitor<AppOptions>(options),
            TimeProvider.System, new MemoryCache(new MemoryCacheOptions()));

    private static AppOptions ConfigRates(params (string Category, int Value)[] rates) =>
        new() { OpenApi = new OpenApiOptions { RateLimits = rates.ToDictionary(r => r.Category, r => r.Value) } };

    private async Task ResetAsync(ServiceProvider sp)
    {
        var db = sp.GetRequiredService<DataContext>();
        await db.Database.MigrateAsync();
        await db.AppSettings.Where(s => s.Key.StartsWith(Core.Constants.OpenApiSettings.RateLimitKeyPrefix))
            .ExecuteDeleteAsync();
    }

    [Fact]
    public async Task Defaults_apply_when_no_config_or_override()
    {
        await using var sp = BuildScope();
        await ResetAsync(sp);

        var effective = await Provider(sp, new AppOptions()).GetEffectiveByNameAsync(CancellationToken.None);

        effective["General"].Should().Be(45);
        effective["HistoricalData"].Should().Be(5);
    }

    [Fact]
    public async Task Config_overrides_default()
    {
        await using var sp = BuildScope();
        await ResetAsync(sp);

        var effective = await Provider(sp, ConfigRates(("General", 100)))
            .GetEffectiveByNameAsync(CancellationToken.None);

        effective["General"].Should().Be(100);      // config wins over default
        effective["HistoricalData"].Should().Be(5);  // untouched category keeps its default
    }

    [Fact]
    public async Task Owner_override_wins_over_config_and_persists_across_instances()
    {
        await using var sp = BuildScope();
        await ResetAsync(sp);

        await Provider(sp, ConfigRates(("General", 100)))
            .SetOwnerOverrideAsync("General", 20, CancellationToken.None);

        // A fresh provider (cold cache) reads the override from AppSettings and it beats the config value.
        var effective = await Provider(sp, ConfigRates(("General", 100)))
            .GetEffectiveByNameAsync(CancellationToken.None);
        effective["General"].Should().Be(20);
    }

    [Fact]
    public async Task Zero_override_persists_as_unlimited()
    {
        await using var sp = BuildScope();
        await ResetAsync(sp);

        await Provider(sp, new AppOptions()).SetOwnerOverrideAsync("HistoricalData", 0, CancellationToken.None);

        var effective = Provider(sp, new AppOptions()).GetEffectiveLimits();
        effective[OpenApiRateCategory.HistoricalData].Should().Be(0);
    }
}
