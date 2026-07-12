using Core.Options;
using Core.WhiteLabel;
using Microsoft.Extensions.Options;

namespace Infrastructure.WhiteLabel;

/// <summary>
/// Decorates the framework <see cref="IOptionsMonitor{AppOptions}"/> so that <see cref="CurrentValue"/> and
/// <see cref="Get"/> return the configuration baseline with owner white-label overrides applied. Because every
/// consumer already reads options through this monitor, an owner override takes effect live for the whole app
/// with no per-consumer change. The applied value is cached by the store's version + the baseline reference,
/// so the hot path is a couple of comparisons when nothing changed. <see cref="Baseline"/> exposes the raw
/// (un-overridden) configuration for the settings snapshot.
/// </summary>
public sealed class WhiteLabelOptionsMonitor : IOptionsMonitor<AppOptions>, IDisposable
{
    private readonly IOptionsMonitor<AppOptions> _inner;
    private readonly WhiteLabelOverrideStore _store;
    private readonly IDisposable? _innerSubscription;
    private readonly Lock _sync = new();
    private readonly List<Action<AppOptions, string?>> _listeners = [];

    private AppOptions? _cached;
    private AppOptions? _cachedBaseline;
    private long _cachedVersion = -1;

    public WhiteLabelOptionsMonitor(IOptionsMonitor<AppOptions> inner, WhiteLabelOverrideStore store)
    {
        _inner = inner;
        _store = store;
        _innerSubscription = inner.OnChange((_, _) => NotifyChanged());
    }

    /// <summary>The raw configuration value with no owner overlay — used to detect an option's provenance.</summary>
    public AppOptions Baseline => _inner.CurrentValue;

    public AppOptions CurrentValue => Effective(_inner.CurrentValue);

    public AppOptions Get(string? name) => Effective(_inner.Get(name));

    public IDisposable OnChange(Action<AppOptions, string?> listener)
    {
        lock (_sync) _listeners.Add(listener);
        return new Subscription(this, listener);
    }

    /// <summary>Recomputes the effective value and notifies listeners; called after an override is written.</summary>
    public void NotifyChanged()
    {
        var value = CurrentValue;
        Action<AppOptions, string?>[] snapshot;
        lock (_sync) snapshot = [.. _listeners];
        foreach (var listener in snapshot) listener(value, null);
    }

    private AppOptions Effective(AppOptions baseline)
    {
        var version = _store.Version;
        lock (_sync)
        {
            if (_cached is not null && _cachedVersion == version && ReferenceEquals(_cachedBaseline, baseline))
                return _cached;

            var applied = WhiteLabelOverlay.Apply(baseline, _store.CurrentDecrypted());
            _cached = applied;
            _cachedBaseline = baseline;
            _cachedVersion = version;
            return applied;
        }
    }

    public void Dispose() => _innerSubscription?.Dispose();

    private sealed class Subscription(WhiteLabelOptionsMonitor owner, Action<AppOptions, string?> listener) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._sync) owner._listeners.Remove(listener);
        }
    }
}
