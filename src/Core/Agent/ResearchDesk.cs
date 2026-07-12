using System.Globalization;
using System.Text;
using Core.Ai;

namespace Core.Agent;

/// <summary>One analyst's view during a research-desk debate.</summary>
public sealed record RoleOpinion(AgentRole Role, string Opinion);

/// <summary>The outcome of a multi-agent debate: each analyst's view, the reviewer's synthesis, and the proposed action.</summary>
public sealed record DebateResult(IReadOnlyList<RoleOpinion> Opinions, string Synthesis, ParsedAgentAction Proposal);

/// <summary>
/// Runs a TradingAgents-style research desk: specialist analysts (Alpha, Sentiment, Technical, Risk)
/// each give a view on the current context, then a Reviewer synthesises them into a single proposed
/// action expressed as JSON (parsed deterministically). Explainable by construction — every opinion is
/// preserved. Gated on the AI provider; degrades to a "not configured" result.
/// </summary>
public interface IResearchDesk
{
    Task<DebateResult> DebateAsync(TradingAgent agent, string context, CancellationToken ct);
}

public sealed class ResearchDesk(IAiClient ai) : IResearchDesk
{
    private const int MaxTokens = 800;
    private static readonly AgentRole[] Analysts = [AgentRole.Alpha, AgentRole.Sentiment, AgentRole.Technical, AgentRole.Risk];

    public async Task<DebateResult> DebateAsync(TradingAgent agent, string context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(agent);
        if (!ai.Enabled)
            return new DebateResult([], "AI is not configured.", new ParsedAgentAction("AI is not configured.", null, null, null, []));

        var persona = agent.CompileSystemPrompt();
        var opinions = new List<RoleOpinion>(Analysts.Length);
        foreach (var role in Analysts)
        {
            var res = await ai.CompleteAsync(new AiTextRequest(RolePrompt(persona, role), context, MaxTokens), ct);
            opinions.Add(new RoleOpinion(role, res.Success ? res.Text.Trim() : res.Error ?? "no response"));
        }

        var review = await ai.CompleteAsync(new AiTextRequest(ReviewerPrompt(persona), ReviewerInput(context, opinions), MaxTokens), ct);
        var synthesis = review.Success ? review.Text.Trim() : review.Error ?? "no synthesis";
        return new DebateResult(opinions, synthesis, AgentDecisionParser.Parse(synthesis));
    }

    private static string RolePrompt(string persona, AgentRole role) => string.Format(
        CultureInfo.InvariantCulture,
        "{0}\nYou are the desk's {1} analyst. {2} Give a concise, specific view; cite the evidence you rely on.",
        persona, role, RoleFocus(role));

    private static string RoleFocus(AgentRole role) => role switch
    {
        AgentRole.Alpha => "Judge the expected edge and directional opportunity.",
        AgentRole.Sentiment => "Judge news and crowd positioning (contrarian where lopsided).",
        AgentRole.Technical => "Judge price structure, regime and momentum.",
        AgentRole.Risk => "Judge the downside, drawdown and whether the risk envelope permits acting.",
        _ => "Give your specialist view."
    };

    private static string ReviewerPrompt(string persona) =>
        persona + "\nYou are the Reviewer. Weigh the analysts' views, resolve disagreement, and output ONLY " +
        "a JSON object: { \"reasoning\": \"...\", \"action\": \"buy|sell|hold\", \"symbol\": \"...\", \"sizeLots\": 0.0, \"evidence\": [\"...\"] }.";

    private static string ReviewerInput(string context, IReadOnlyList<RoleOpinion> opinions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Context:").AppendLine(context).AppendLine().AppendLine("Analyst views:");
        foreach (var o in opinions) sb.Append(o.Role).Append(": ").AppendLine(o.Opinion);
        return sb.ToString();
    }
}
