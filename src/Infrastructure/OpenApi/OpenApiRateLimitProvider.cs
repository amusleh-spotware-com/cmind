using System.Globalization;
using Core;
using Core.Constants;
using Core.Options;
using CTraderOpenApi.RateLimiting;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.OpenApi;

/// <summary>
/// Resolves the effective per-message-type Open API rate limits from (precedence, highest last): built-in
/// safe defaults → deployment config (<c>App:OpenApi:RateLimits</c>) → owner runtime overrides stored in
/// <c>AppSettings</c>. Overrides are cached briefly so the connection-build path stays cheap.
/// </summary>
public interface IOpenApiRateLimitProvider
{
    IReadOnlyDictionary<OpenApiRateCategory, int> GetEffectiveLimits();
    Task<IReadOnlyDictionary<string, int>> GetEffectiveByNameAsync(CancellationToken ct);
    Task SetOwnerOverrideAsync(string category, int value, CancellationToken ct);
}

public sealed class OpenApiRateLimitProvider(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider,
    IMemoryCache cache) : IOpenApiRateLimitProvider
{
    private static readonly IReadOnlyDictionary<OpenApiRateCategory, int> Defaults =
        new Dictionary<OpenApiRateCategory, int>
        {
            [OpenApiRateCategory.General] = 45,
            [OpenApiRateCategory.HistoricalData] = 5
        };

    public IReadOnlyDictionary<OpenApiRateCategory, int> GetEffectiveLimits()
    {
        var overrides = cache.GetOrCreate(OpenApiSettings.RateLimitCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = OpenApiSettings.CacheTtl;
            return LoadOverrides();
        }) ?? new Dictionary<string, int>();

        var config = options.CurrentValue.OpenApi.RateLimits;
        var result = new Dictionary<OpenApiRateCategory, int>();
        foreach (var (category, defaultRate) in Defaults)
        {
            var name = category.ToString();
            var rate = defaultRate;
            if (config.TryGetValue(name, out var configured)) rate = configured;
            if (overrides.TryGetValue(name, out var owned)) rate = owned;
            result[category] = rate;
        }
        return result;
    }

    public Task<IReadOnlyDictionary<string, int>> GetEffectiveByNameAsync(CancellationToken ct)
    {
        var effective = GetEffectiveLimits()
            .ToDictionary(pair => pair.Key.ToString(), pair => pair.Value);
        return Task.FromResult<IReadOnlyDictionary<string, int>>(effective);
    }

    public async Task SetOwnerOverrideAsync(string category, int value, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var key = OpenApiSettings.RateLimitKey(category);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        var now = timeProvider.GetUtcNow();
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (setting is null)
            db.AppSettings.Add(AppSetting.Create(key, text, now));
        else
            setting.SetValue(text, now);
        await db.SaveChangesAsync(ct);
        cache.Remove(OpenApiSettings.RateLimitCacheKey);
    }

    private Dictionary<string, int> LoadOverrides()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var prefix = OpenApiSettings.RateLimitKeyPrefix;
        var rows = db.AppSettings.Where(s => s.Key.StartsWith(prefix)).ToList();
        var map = new Dictionary<string, int>();
        foreach (var row in rows)
            if (int.TryParse(row.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rate))
                map[row.Key[prefix.Length..]] = rate;
        return map;
    }
}
