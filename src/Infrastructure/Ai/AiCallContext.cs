using Core.Ai;

namespace Infrastructure.Ai;

/// <summary>Scoped, mutable implementation of <see cref="IAiCallContext"/> — one per request scope.</summary>
public sealed class AiCallContext : IAiCallContext
{
    public Core.AiProviderCredentialId? OverrideCredentialId { get; set; }
}
