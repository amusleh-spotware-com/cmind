namespace Core.WhiteLabel;

/// <summary>
/// One deployment (white-label) option the app owner can inspect and override at runtime, exactly as a
/// deployment sets it through <c>appsettings</c>/env. The catalog (<see cref="WhiteLabelCatalog"/>) is the
/// single source of truth: the Owner UI renders from it, the overlay applies overrides described by it, and
/// a parity test asserts every white-label options property is covered by one of these.
/// </summary>
public sealed record WhiteLabelOption
{
    /// <summary>Stable dotted id, e.g. <c>branding.requireMfa</c>. Also the override key tail.</summary>
    public required string Key { get; init; }

    /// <summary>
    /// Property path on <c>AppOptions</c>, e.g. <c>Branding.RequireMfa</c> or <c>Registration.Captcha.Secret</c>.
    /// Drives the overlay, source detection and the parity/reflection checks.
    /// </summary>
    public required string PropertyPath { get; init; }

    public required WhiteLabelValueKind Kind { get; init; }
    public required WhiteLabelCategory Category { get; init; }

    /// <summary>Short English label shown in the UI (rendered as data, not a literal — see localization note).</summary>
    public required string Label { get; init; }

    /// <summary>English help text surfaced through the HelpTip.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>False ⇒ shown read-only (managed elsewhere or restart-only).</summary>
    public bool OwnerEditable { get; init; } = true;

    /// <summary>Encrypted at rest, write-only in the UI, never returned to a client.</summary>
    public bool IsSecret { get; init; }

    /// <summary>Persisted through <c>IFeatureGate</c> (the existing feature-override store), not the overlay.</summary>
    public bool IsFeatureFlag { get; init; }

    /// <summary>For <see cref="WhiteLabelValueKind.Enum"/>: the enum type whose names are the valid values.</summary>
    public Type? EnumType { get; init; }

    /// <summary>When set, the option is edited on another settings surface (e.g. the Open API section).</summary>
    public string? DelegatedToSection { get; init; }

    /// <summary>The cTrader deployment config key an operator would set, e.g. <c>App:Branding:RequireMfa</c>.</summary>
    public string ConfigKey => "App:" + PropertyPath.Replace('.', ':');
}

/// <summary>Where an option's effective value comes from.</summary>
public enum WhiteLabelValueSource
{
    /// <summary>No override and the config equals the built-in default.</summary>
    Default,

    /// <summary>No owner override; the deployment configuration set it.</summary>
    Config,

    /// <summary>An owner runtime override is in effect.</summary>
    Owner
}

/// <summary>The catalog entry plus its current effective value and provenance, for the Owner UI/API.</summary>
public sealed record WhiteLabelEffectiveValue
{
    public required WhiteLabelOption Option { get; init; }

    /// <summary>Effective value in string form. <c>null</c> for secrets (never returned).</summary>
    public string? Value { get; init; }

    /// <summary>For secrets: whether a value is set (config or override) without revealing it.</summary>
    public bool HasValue { get; init; }

    public required WhiteLabelValueSource Source { get; init; }

    /// <summary>Whether an owner runtime override is currently set for this option.</summary>
    public bool HasOverride { get; init; }
}
