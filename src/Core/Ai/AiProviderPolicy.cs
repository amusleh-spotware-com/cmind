using Core.Constants;
using Core.Domain;
using Core.Options;

namespace Core.Ai;

/// <summary>
/// White-label policy over which AI providers a deployment permits (from <c>App:Branding</c>): the
/// built-in local LLM can be removed, local/self-hosted endpoints can be forbidden, and the allowed
/// kinds can be restricted to a sanctioned set. Enforced server-side on every provider upsert.
/// </summary>
public static class AiProviderPolicy
{
    public static bool IsKindAllowed(AiProviderKind kind, BrandingOptions branding)
    {
        if (kind == AiProviderKind.BuiltInOnnx && !branding.AllowBuiltInAi) return false;
        var allowed = branding.AllowedAiProviderKinds;
        return allowed.Count == 0
            || allowed.Any(k => string.Equals(k, kind.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<AiProviderKind> AllowedKinds(BrandingOptions branding) =>
        Enum.GetValues<AiProviderKind>().Where(k => IsKindAllowed(k, branding)).ToList();

    public static void EnsureAllowed(AiProviderKind kind, AiEndpoint endpoint, BrandingOptions branding)
    {
        if (kind == AiProviderKind.BuiltInOnnx && !branding.AllowBuiltInAi)
            throw new DomainException(DomainErrors.AiBuiltInNotAllowed);

        if (!IsKindAllowed(kind, branding))
            throw new DomainException(DomainErrors.AiProviderKindNotAllowed);

        // A local/self-hosted OpenAI-compatible endpoint is a "local provider"; block it when disallowed.
        if (!branding.AllowLocalProviders && kind == AiProviderKind.OpenAiCompatible && endpoint.IsLocal)
            throw new DomainException(DomainErrors.AiLocalProviderNotAllowed);
    }
}
