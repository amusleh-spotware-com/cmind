namespace Core.Ai;

/// <summary>
/// Per-request holder for a user-chosen AI model override. A Web endpoint sets
/// <see cref="OverrideCredentialId"/> from the model the user picked in a feature page's model selector,
/// so a single AI action runs on that exact provider credential without threading a parameter through
/// every <see cref="IAiFeatureService"/> method. <c>RoutingAiClient</c> reads it as the highest-priority
/// resolution source after an explicit request credential. Scoped (one per request); background services
/// run in their own scope where it is unset, so they fall back to the feature binding / default provider.
/// </summary>
public interface IAiCallContext
{
    Core.AiProviderCredentialId? OverrideCredentialId { get; set; }
}
