using System.Text;
using Core.Ai;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Built-in in-process fake provider: returns a canned, prompt-aware response with no external endpoint
/// and no key. Lets anyone activate "Demo AI" in Settings → AI and see how the AI features work before
/// wiring a real provider. Never touches the network, so it is instant and always "available".
/// </summary>
public sealed class DemoAiProvider : IAiProvider
{
    public const string Marker = "cMind Demo AI";

    public AiProviderKind Kind => AiProviderKind.Demo;

    public Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🤖 {Marker} — this is a built-in demo model (no external LLM, no API key).");
        sb.AppendLine("Wire a real provider in Settings → AI to get genuine analysis.");
        sb.AppendLine();
        sb.AppendLine("Demo assessment:");
        sb.AppendLine("• The request was received and processed by the demo model.");
        sb.AppendLine("• Consider risk limits, stop-losses, and out-of-sample validation before going live.");
        if (request.Image is not null)
            sb.AppendLine("• An image was attached and would be analysed by a vision-capable provider.");
        if (request.EnableWebSearch)
            sb.AppendLine("• Web search would ground this answer in current data on a search-capable provider.");

        var prompt = string.IsNullOrWhiteSpace(request.User) ? request.System : request.User;
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            sb.AppendLine();
            sb.AppendLine("You asked:");
            sb.Append(prompt.Length <= 240 ? prompt : prompt[..240] + "…");
        }

        return Task.FromResult(AiResult.Ok(sb.ToString().Trim()));
    }
}
