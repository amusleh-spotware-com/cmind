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
    private readonly Dictionary<AiProviderKind, IAiProvider> _providers;

    public RoutingAiClient(IAiProviderStore store, IEnumerable<IAiProvider> providers)
    {
        _store = store;
        _providers = providers.ToDictionary(p => p.Kind);
    }

    public bool Enabled => _store.HasActive;

    public Task<AiResult> CompleteAsync(AiTextRequest request, CancellationToken ct)
    {
        // Route to the provider bound to this request's feature (or the caller-forced credential), falling
        // back to the scope's active provider when the feature is unbound.
        if (_store.ResolveFor(request.Feature, request.CredentialId) is not { } active)
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
