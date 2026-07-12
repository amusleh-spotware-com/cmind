namespace Core.WhiteLabel;

/// <summary>
/// Owner-facing read/write surface for white-label options: the effective value of every catalogued option
/// (deployment config baseline overlaid with an owner runtime override) and the ability to set/clear those
/// overrides. Feature-flag options are delegated to <see cref="Core.Features.IFeatureGate"/>; every other
/// override is applied on top of <c>AppOptions</c> so it takes effect without a redeploy.
/// </summary>
public interface IWhiteLabelSettings
{
    /// <summary>Every catalogued option with its current effective value and provenance (secrets masked).</summary>
    Task<IReadOnlyList<WhiteLabelEffectiveValue>> SnapshotAsync(CancellationToken ct);

    /// <summary>
    /// Sets an owner override for <paramref name="key"/>. <paramref name="rawValue"/> is the raw string form
    /// (validated against the option's kind); <c>null</c> clears the override so the option reverts to config.
    /// For a secret option a blank value keeps the existing secret. Throws <see cref="Core.Domain.DomainException"/>
    /// on an unknown key, a non-editable option, or an invalid value.
    /// </summary>
    Task SetOverrideAsync(string key, string? rawValue, CancellationToken ct);

    /// <summary>Clears the override for a single option, reverting it to the deployment configuration baseline.</summary>
    Task ClearOverrideAsync(string key, CancellationToken ct);

    /// <summary>Clears every owner override, reverting the whole deployment to its pure configuration.</summary>
    Task ClearAllOverridesAsync(CancellationToken ct);
}
