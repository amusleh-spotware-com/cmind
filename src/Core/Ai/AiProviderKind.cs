namespace Core.Ai;

/// <summary>
/// The wire families the AI layer speaks. Ollama, LM Studio, vLLM, llama.cpp, LocalAI, OpenRouter,
/// Groq, Together, Mistral, DeepSeek and every other OpenAI-compatible runtime are <b>not</b> distinct
/// kinds — they are <see cref="OpenAiCompatible"/> with a different base URL. This keeps the routing
/// switch small while covering the entire long tail (cloud and local).
/// </summary>
public enum AiProviderKind
{
    Anthropic,
    OpenAiCompatible,
    AzureOpenAi,
    Gemini,

    /// <summary>
    /// Built-in in-process fake — returns canned, prompt-aware responses with no external endpoint and
    /// no key. Lets anyone try the AI features live to see how they work before wiring a real provider.
    /// </summary>
    Demo,

    /// <summary>
    /// Built-in real local LLM running in-process via Microsoft.ML.OnnxRuntimeGenAI (e.g. Phi-3.5-mini).
    /// Shipped with the app and enabled by default, so every user gets working AI with no API key or
    /// external service. A white-label deployment can disable it (<c>App:Branding:AllowBuiltInAi</c>).
    /// </summary>
    BuiltInOnnx
}
