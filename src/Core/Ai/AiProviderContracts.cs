namespace Core.Ai;

/// <summary>
/// The normalized request handed to a provider adapter. It carries the feature-neutral
/// <see cref="AiTextRequest"/> content plus the fully-resolved target (kind, base URL, model, key,
/// capabilities, token budget) so an adapter is stateless with respect to configuration — the
/// <c>RoutingAiClient</c> resolves the active credential and injects everything here.
/// </summary>
public sealed record AiProviderRequest(
    AiProviderKind Kind,
    string BaseUrl,
    string Model,
    string? ApiKey,
    int MaxTokens,
    AiProviderCapabilities Capabilities,
    string System,
    string User,
    bool EnableWebSearch,
    AiImage? Image);

/// <summary>
/// Internal port below <see cref="IAiClient"/>: one implementation per wire format. Adapters translate
/// the normalized request to their provider's request/response shape and always degrade to a typed
/// <see cref="AiResult"/> failure rather than throw.
/// </summary>
public interface IAiProvider
{
    AiProviderKind Kind { get; }
    Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct);
}

/// <summary>
/// The active provider resolved from the credential store, with the API key decrypted, ready to build
/// an <see cref="AiProviderRequest"/>. A <c>null</c> <see cref="ApiKey"/> is valid — local endpoints
/// need none.
/// </summary>
public sealed record ActiveAiProvider(
    AiProviderKind Kind,
    string BaseUrl,
    string Model,
    string? ApiKey,
    AiProviderCapabilities Capabilities,
    int MaxTokens);

/// <summary>Redacted view of a stored provider credential for the management UI (never exposes the key).</summary>
public sealed record AiProviderView(
    Guid Id,
    AiProviderKind Kind,
    string BaseUrl,
    string Model,
    bool HasKey,
    bool IsActive,
    int MaxTokens,
    AiProviderCapabilities Capabilities);

/// <summary>Command to create or update a provider credential (id absent = create).</summary>
public sealed record UpsertAiProviderCommand(
    Guid? Id,
    AiProviderKind Kind,
    string BaseUrl,
    string Model,
    string? ApiKey,
    int? MaxTokens,
    AiProviderCapabilities? Capabilities,
    bool Activate);

/// <summary>
/// Owner-managed store of AI provider credentials. Supersedes the single-key store: N providers may be
/// stored, exactly one is active. Keys are persisted encrypted; reads of the active provider are cached
/// so gating stays cheap on the request path.
/// </summary>
public interface IAiProviderStore
{
    bool HasActive { get; }
    ActiveAiProvider? Active { get; }
    Task<IReadOnlyList<AiProviderView>> ListAsync(CancellationToken ct);
    Task<Guid> UpsertAsync(UpsertAiProviderCommand command, CancellationToken ct);
    Task ActivateAsync(Guid id, CancellationToken ct);
    Task RemoveAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Idempotently imports any deployment-seeded providers (<c>App:Ai:Providers[]</c>) into the store if
    /// no credential rows exist yet, so an ops team can ship a configured (incl. local-LLM) deployment
    /// purely via appsettings/env. Runs once at startup; a no-op when providers already exist.
    /// </summary>
    Task SeedFromConfigAsync(CancellationToken ct);
}
