namespace Core.Features;

/// <summary>
/// Resolves whether a <see cref="FeatureFlag"/> is enabled for the running deployment. The effective value
/// is the deployment configuration baseline (<c>App:Features</c>) overlaid with an optional owner-set runtime
/// override, so a feature can be shipped off and switched on later without a redeploy.
/// </summary>
public interface IFeatureGate
{
    bool IsEnabled(FeatureFlag flag);

    IReadOnlyDictionary<FeatureFlag, bool> Snapshot();

    /// <summary>
    /// Sets a runtime override for <paramref name="flag"/>. Passing <c>null</c> clears the override so the
    /// feature reverts to the configuration baseline.
    /// </summary>
    Task SetOverrideAsync(FeatureFlag flag, bool? enabled, CancellationToken ct);
}
