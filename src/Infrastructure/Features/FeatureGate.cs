using Core;
using Core.Constants;
using Core.Features;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Infrastructure.Features;

public sealed class FeatureGate(
    IOptionsMonitor<AppOptions> options,
    DataContext db,
    IMemoryCache cache,
    TimeProvider timeProvider) : IFeatureGate
{
    public bool IsEnabled(FeatureFlag flag)
    {
        var overrides = LoadOverrides();
        return overrides.TryGetValue(flag, out var value)
            ? value
            : options.CurrentValue.Features.IsEnabled(flag);
    }

    public IReadOnlyDictionary<FeatureFlag, bool> Snapshot()
    {
        var overrides = LoadOverrides();
        var baseline = options.CurrentValue.Features;
        return Enum.GetValues<FeatureFlag>()
            .ToDictionary(flag => flag, flag => overrides.TryGetValue(flag, out var value) ? value : baseline.IsEnabled(flag));
    }

    public async Task SetOverrideAsync(FeatureFlag flag, bool? enabled, CancellationToken ct)
    {
        var key = FeatureSettings.OverrideKey(flag);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (enabled is null)
        {
            if (setting is not null) db.AppSettings.Remove(setting);
        }
        else
        {
            var text = enabled.Value ? bool.TrueString : bool.FalseString;
            if (setting is null)
                db.AppSettings.Add(AppSetting.Create(key, text, timeProvider.GetUtcNow()));
            else
                setting.SetValue(text, timeProvider.GetUtcNow());
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(FeatureSettings.OverrideCacheKey);
    }

    // Overrides are loaded inline (not via a deferred cache factory) so the scoped DataContext is only ever
    // touched within the current request scope; the cached value is a plain snapshot dictionary with no EF
    // references, safe to share across the short TTL.
    private Dictionary<FeatureFlag, bool> LoadOverrides()
    {
        if (cache.TryGetValue(FeatureSettings.OverrideCacheKey, out Dictionary<FeatureFlag, bool>? cached)
            && cached is not null)
            return cached;

        var result = new Dictionary<FeatureFlag, bool>();
        var rows = db.AppSettings.AsNoTracking()
            .Where(s => s.Key.StartsWith(FeatureSettings.OverrideKeyPrefix))
            .ToList();
        foreach (var row in rows)
        {
            var name = row.Key[FeatureSettings.OverrideKeyPrefix.Length..];
            if (Enum.TryParse<FeatureFlag>(name, out var flag) && bool.TryParse(row.Value, out var value))
                result[flag] = value;
        }

        cache.Set(FeatureSettings.OverrideCacheKey, result, FeatureSettings.OverrideCacheTtl);
        return result;
    }
}
