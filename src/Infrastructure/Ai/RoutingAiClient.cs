using Core.Ai;
using Core.Constants;

namespace Infrastructure.Ai;

/// <summary>
/// The single <see cref="IAiClient"/> the whole app depends on. It preserves the provider-neutral seam:
/// resolves the active provider credential, picks the matching <see cref="IAiProvider"/> adapter by
/// <see cref="AiProviderKind"/>, injects the resolved key/base URL/model/capabilities into a normalized
/// <see cref="AiProviderRequest"/>, and delegates. Capability gaps degrade — web search is dropped when
/// unsupported; vision returns a typed failure — so a feature never throws for an unsupported request.
/// </summary>
public sealed class RoutingAiClient : IAiClient
{
    private readonly IAiProviderStore _store;
    private readonly IAiCallContext _callContext;
    private readonly Dictionary<AiProviderKind, IAiProvider> _providers;

    public RoutingAiClient(IAiProviderStore store, IAiCallContext callContext, IEnumerable<IAiProvider> providers)
    {
        _store = store;
        _callContext = callContext;
        _providers = providers.ToDictionary(p => p.Kind);
    }

    // Convenience for callers with no per-request model override (tests) — an empty call context.
    public RoutingAiClient(IAiProviderStore store, IEnumerable<IAiProvider> providers)
        : this(store, new AiCallContext(), providers)
    {
    }

    public bool Enabled => _store.HasActive;

    public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
    {
        // Resolution priority: an explicit request credential (build/fix flow) wins, then the user's
        // per-request model selection (a feature page's model dropdown, carried on the scoped call
        // context), then the per-feature binding, then the scope's active/default provider.
        var credentialId = request.CredentialId ?? _callContext.OverrideCredentialId;
        if (_store.ResolveFor(request.Feature, credentialId) is not { } active)
            return Task.FromResult(AiResult.Fail(AiConstants.DisabledMessage));
        if (!_providers.TryGetValue(active.Kind, out var provider))
            return Task.FromResult(AiResult.Fail(AiConstants.DisabledMessage));

        var caps = active.Capabilities;
        if (request.Image is not null && !caps.SupportsVision)
            return Task.FromResult(AiResult.Fail(AiConstants.VisionUnsupportedMessage));

        var enableWebSearch = request.EnableWebSearch && caps.SupportsWebSearch;
        var providerRequest = new AiProviderRequest(
            active.Kind, active.BaseUrl, active.Model, active.ApiKey,
            request.MaxTokens ?? active.MaxTokens, caps,
            request.System, request.User, enableWebSearch, request.Image);

        return provider.CompleteAsync(providerRequest, ct);
    }
}
