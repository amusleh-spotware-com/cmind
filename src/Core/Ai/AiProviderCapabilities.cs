namespace Core.Ai;

/// <summary>
/// What a configured provider can do beyond plain text completion. Computed from the
/// <see cref="AiProviderKind"/> by default and overridable by the owner (a local model behind an
/// OpenAI-compatible endpoint may or may not accept images or a system role). When a capability is
/// off, the feature degrades — never throws: web search is silently dropped; vision returns a typed
/// capability-unsupported failure.
/// </summary>
public sealed record AiProviderCapabilities(
    bool SupportsWebSearch,
    bool SupportsVision,
    bool SupportsSystemRole,
    bool SupportsTools)
{
    public static AiProviderCapabilities DefaultFor(AiProviderKind kind) => kind switch
    {
        // Anthropic: server-side web search tool + vision + native system role.
        AiProviderKind.Anthropic => new(SupportsWebSearch: true, SupportsVision: true, SupportsSystemRole: true, SupportsTools: true),
        // Cloud OpenAI is capable, but this kind also covers every local runtime — default to the safe
        // lowest-common-denominator (text-only) and let the owner opt vision/search on for a cloud target.
        AiProviderKind.OpenAiCompatible => new(SupportsWebSearch: false, SupportsVision: false, SupportsSystemRole: true, SupportsTools: false),
        AiProviderKind.AzureOpenAi => new(SupportsWebSearch: false, SupportsVision: true, SupportsSystemRole: true, SupportsTools: false),
        AiProviderKind.Gemini => new(SupportsWebSearch: true, SupportsVision: true, SupportsSystemRole: true, SupportsTools: true),
        // Demo pretends to support everything so every feature (incl. vision) renders a canned response.
        AiProviderKind.Demo => new(SupportsWebSearch: true, SupportsVision: true, SupportsSystemRole: true, SupportsTools: true),
        // Built-in ONNX is a compact text model: text-only (no web search / vision), native system role.
        AiProviderKind.BuiltInOnnx => new(SupportsWebSearch: false, SupportsVision: false, SupportsSystemRole: true, SupportsTools: false),
        _ => new(SupportsWebSearch: false, SupportsVision: false, SupportsSystemRole: true, SupportsTools: false)
    };
}
