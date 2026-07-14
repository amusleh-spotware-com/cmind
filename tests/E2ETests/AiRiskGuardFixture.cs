using System.Diagnostics;
using Xunit;

namespace E2ETests;

// An AI-configured fixture (fake local LLM) running the AI risk-guard worker in ASSESSMENT-ONLY mode
// (auto-stop OFF) on a fast interval. Assessment-only never stops an instance or touches a container — it
// calls the AI on the running bots and logs the summary — so it is a clean, non-destructive surface for
// proving the risk-guard's AI path end to end. Own collection so its worker load stays isolated.
public sealed class AiRiskGuardFixture : AiLocalFixture
{
    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        base.ConfigureApp(psi);
        psi.Environment["App__Ai__RiskGuardEnabled"] = "true";
        psi.Environment["App__Ai__RiskGuardAutoStop"] = "false";
        psi.Environment["App__Ai__RiskGuardInterval"] = "00:00:02";
    }
}

[CollectionDefinition(Name)]
public sealed class AiRiskGuardCollection : ICollectionFixture<AiRiskGuardFixture>
{
    public const string Name = "ai-riskguard";
}
