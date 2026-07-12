using System.Globalization;
using Core;
using Core.Branding;
using Core.Constants;
using Core.Features;
using Core.Options;
using Core.WhiteLabel;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.WhiteLabel;

/// <summary>
/// Owner-facing white-label settings service: builds the effective-value snapshot (config baseline vs owner
/// override vs default) and applies/clears overrides. Non-feature overrides go through
/// <see cref="WhiteLabelOverrideStore"/> (which feeds the options overlay); feature-flag options delegate to
/// the existing <see cref="IFeatureGate"/> so the two never diverge. Singleton; resolves scoped services
/// (feature gate, database) per call through the scope factory.
/// </summary>
public sealed class WhiteLabelSettings(
    WhiteLabelOptionsMonitor monitor,
    WhiteLabelOverrideStore store,
    ISecretProtector secretProtector,
    IServiceScopeFactory scopeFactory) : IWhiteLabelSettings
{
    private static readonly AppOptions Defaults = new();
    private const string FeaturePrefix = "feature.";

    public async Task<IReadOnlyList<WhiteLabelEffectiveValue>> SnapshotAsync(CancellationToken ct)
    {
        var baseline = monitor.Baseline;
        var effective = monitor.CurrentValue;
        var overrideKeys = store.CurrentKeys();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var featureGate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();
        var featureOverridden = (await db.AppSettings.AsNoTracking()
                .Where(s => s.Key.StartsWith(FeaturePrefix))
                .Select(s => s.Key)
                .ToListAsync(ct))
            .Select(k => k[FeaturePrefix.Length..])
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<WhiteLabelEffectiveValue>(WhiteLabelCatalog.All.Count);
        foreach (var option in WhiteLabelCatalog.All)
        {
            if (option.IsFeatureFlag)
            {
                var flag = ParseFlag(option);
                var enabled = featureGate.IsEnabled(flag);
                var hasOverride = featureOverridden.Contains(flag.ToString());
                var source = hasOverride
                    ? WhiteLabelValueSource.Owner
                    : baseline.Features.IsEnabled(flag) != Defaults.Features.IsEnabled(flag)
                        ? WhiteLabelValueSource.Config
                        : WhiteLabelValueSource.Default;
                result.Add(new WhiteLabelEffectiveValue
                {
                    Option = option,
                    Value = enabled ? bool.TrueString : bool.FalseString,
                    HasValue = true,
                    Source = source,
                    HasOverride = hasOverride
                });
                continue;
            }

            var hasRowOverride = overrideKeys.Contains(option.Key);
            var effectiveRaw = WhiteLabelOverlay.ReadRaw(effective, option);
            var baselineRaw = WhiteLabelOverlay.ReadRaw(baseline, option);
            var defaultRaw = WhiteLabelOverlay.ReadRaw(Defaults, option);
            var source = hasRowOverride
                ? WhiteLabelValueSource.Owner
                : !string.Equals(baselineRaw, defaultRaw, StringComparison.Ordinal)
                    ? WhiteLabelValueSource.Config
                    : WhiteLabelValueSource.Default;

            result.Add(new WhiteLabelEffectiveValue
            {
                Option = option,
                Value = option.IsSecret ? null : effectiveRaw,
                HasValue = !string.IsNullOrEmpty(effectiveRaw),
                Source = source,
                HasOverride = hasRowOverride
            });
        }

        return result;
    }

    public async Task SetOverrideAsync(string key, string? rawValue, CancellationToken ct)
    {
        if (!WhiteLabelCatalog.TryGet(key, out var option))
            throw new Core.Domain.DomainException(DomainErrors.WhiteLabelOptionUnknown);
        if (!option.OwnerEditable)
            throw new Core.Domain.DomainException(DomainErrors.WhiteLabelOptionNotEditable);

        if (option.IsFeatureFlag)
        {
            var flag = ParseFlag(option);
            bool? value = rawValue is null ? null : ParseBoolStrict(rawValue);
            await using var scope = scopeFactory.CreateAsyncScope();
            var featureGate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();
            await featureGate.SetOverrideAsync(flag, value, ct);
            monitor.NotifyChanged();
            return;
        }

        if (rawValue is null)
        {
            await store.RemoveAsync(key, ct);
            monitor.NotifyChanged();
            return;
        }

        if (option.IsSecret)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return; // blank keeps the existing secret
            var cipher = secretProtector.ProtectString(rawValue, EncryptionPurposes.WhiteLabelSecret);
            await store.UpsertAsync(key, cipher, ct);
            monitor.NotifyChanged();
            return;
        }

        Validate(option, rawValue);
        await store.UpsertAsync(key, rawValue, ct);
        monitor.NotifyChanged();
    }

    public async Task ClearOverrideAsync(string key, CancellationToken ct)
    {
        if (!WhiteLabelCatalog.TryGet(key, out var option))
            throw new Core.Domain.DomainException(DomainErrors.WhiteLabelOptionUnknown);

        if (option.IsFeatureFlag)
        {
            var flag = ParseFlag(option);
            await using var scope = scopeFactory.CreateAsyncScope();
            var featureGate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();
            await featureGate.SetOverrideAsync(flag, null, ct);
        }
        else
        {
            await store.RemoveAsync(key, ct);
        }

        monitor.NotifyChanged();
    }

    public async Task ClearAllOverridesAsync(CancellationToken ct)
    {
        await store.RemoveAllAsync(ct);
        await using var scope = scopeFactory.CreateAsyncScope();
        var featureGate = scope.ServiceProvider.GetRequiredService<IFeatureGate>();
        foreach (var flag in System.Enum.GetValues<FeatureFlag>())
            await featureGate.SetOverrideAsync(flag, null, ct);
        monitor.NotifyChanged();
    }

    private static FeatureFlag ParseFlag(WhiteLabelOption option) =>
        System.Enum.Parse<FeatureFlag>(option.PropertyPath["Features.".Length..]);

    private static bool ParseBoolStrict(string raw) =>
        bool.TryParse(raw, out var value)
            ? value
            : throw new Core.Domain.DomainException(DomainErrors.WhiteLabelValueInvalid);

    private static void Validate(WhiteLabelOption option, string raw)
    {
        switch (option.Kind)
        {
            case WhiteLabelValueKind.Bool:
                if (!bool.TryParse(raw, out _)) Invalid();
                break;
            case WhiteLabelValueKind.Int:
                if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) Invalid();
                break;
            case WhiteLabelValueKind.Number:
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) Invalid();
                break;
            case WhiteLabelValueKind.TimeSpan:
                if (!TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out _)) Invalid();
                break;
            case WhiteLabelValueKind.Enum:
                if (option.EnumType is null
                    || !System.Enum.TryParse(option.EnumType, raw, ignoreCase: true, out var parsed)
                    || parsed is null
                    || !System.Enum.IsDefined(option.EnumType, parsed))
                    Invalid();
                break;
            case WhiteLabelValueKind.Color:
                if (!string.IsNullOrWhiteSpace(raw)) _ = new HexColor(raw); // throws on invalid
                break;
            case WhiteLabelValueKind.String:
            case WhiteLabelValueKind.MultilineString:
            case WhiteLabelValueKind.StringList:
            case WhiteLabelValueKind.Secret:
                break;
            default:
                Invalid();
                break;
        }

        static void Invalid() => throw new Core.Domain.DomainException(DomainErrors.WhiteLabelValueInvalid);
    }
}
