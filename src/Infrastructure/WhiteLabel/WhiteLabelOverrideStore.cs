using Core;
using Core.Constants;
using Core.WhiteLabel;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.WhiteLabel;

/// <summary>
/// Persists and caches owner white-label overrides as <c>AppSetting</c> rows keyed
/// <c>whitelabel.&lt;optionKey&gt;</c>. Supplies the decrypted, non-feature override map that feeds the
/// options overlay, and a monotonically increasing <see cref="Version"/> so the decorated options monitor can
/// cheaply detect changes. Secrets are stored as ciphertext and decrypted on load. Singleton; resolves a
/// scoped <see cref="DataContext"/> per operation through the scope factory (safe from any lifetime).
/// </summary>
public sealed class WhiteLabelOverrideStore(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    ISecretProtector secretProtector,
    TimeProvider timeProvider)
{
    private readonly Lock _sync = new();
    private long _version;
    private Dictionary<string, string> _lastMap = new(StringComparer.Ordinal);

    private static readonly HashSet<string> SecretKeys =
        WhiteLabelCatalog.All.Where(o => o.IsSecret).Select(o => o.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>Bumped whenever the effective override content changes; the options monitor caches by this.</summary>
    public long Version
    {
        get
        {
            EnsureLoaded();
            return Interlocked.Read(ref _version);
        }
    }

    /// <summary>Decrypted override values keyed by option key (feature flags excluded — those live on IFeatureGate).</summary>
    public IReadOnlyDictionary<string, string> CurrentDecrypted()
    {
        EnsureLoaded();
        lock (_sync) return _lastMap;
    }

    /// <summary>Option keys that currently carry an owner override (non-feature).</summary>
    public IReadOnlySet<string> CurrentKeys()
    {
        EnsureLoaded();
        lock (_sync) return _lastMap.Keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task UpsertAsync(string optionKey, string storedValue, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var key = WhiteLabelSettingsKeys.OverrideKey(optionKey);
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
            db.AppSettings.Add(AppSetting.Create(key, storedValue, timeProvider.GetUtcNow()));
        else
            row.SetValue(storedValue, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task RemoveAsync(string optionKey, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var key = WhiteLabelSettingsKeys.OverrideKey(optionKey);
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null) return;
        db.AppSettings.Remove(row);
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task RemoveAllAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var rows = await db.AppSettings
            .Where(s => s.Key.StartsWith(WhiteLabelSettingsKeys.OverrideKeyPrefix))
            .ToListAsync(ct);
        if (rows.Count == 0) return;
        db.AppSettings.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public void Invalidate() => cache.Remove(WhiteLabelSettingsKeys.OverrideCacheKey);

    private void EnsureLoaded()
    {
        if (cache.TryGetValue(WhiteLabelSettingsKeys.OverrideCacheKey, out _)) return;

        lock (_sync)
        {
            if (cache.TryGetValue(WhiteLabelSettingsKeys.OverrideCacheKey, out _)) return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var rows = db.AppSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith(WhiteLabelSettingsKeys.OverrideKeyPrefix))
                .ToList();

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var optionKey = row.Key[WhiteLabelSettingsKeys.OverrideKeyPrefix.Length..];
                var value = row.Value;
                if (SecretKeys.Contains(optionKey))
                {
                    try { value = secretProtector.UnprotectString(row.Value, EncryptionPurposes.WhiteLabelSecret); }
                    catch { continue; }
                }
                map[optionKey] = value;
            }

            if (!MapsEqual(map, _lastMap))
            {
                _lastMap = map;
                Interlocked.Increment(ref _version);
            }

            cache.Set(WhiteLabelSettingsKeys.OverrideCacheKey, true, WhiteLabelSettingsKeys.OverrideCacheTtl);
        }
    }

    private static bool MapsEqual(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var (key, value) in a)
            if (!b.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        return true;
    }
}
