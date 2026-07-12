using Core.Constants;
using Core.Domain;

namespace Core.Ai;

/// <summary>
/// The model string a provider is asked to run — <c>claude-opus-4-8</c>, <c>gpt-4o</c>,
/// <c>gemini-2.0-flash</c>, <c>llama3.1:8b</c>, <c>qwen2.5-coder</c>. Non-empty, trimmed.
/// </summary>
public readonly record struct AiModelId
{
    public string Value { get; }

    public AiModelId(string value)
        => Value = DomainGuard.AgainstNullOrWhiteSpace(value, DomainErrors.AiModelRequired);

    public override string ToString() => Value;
}
