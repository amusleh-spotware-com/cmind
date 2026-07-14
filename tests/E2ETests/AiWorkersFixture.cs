using System.Diagnostics;
using Xunit;

namespace E2ETests;

// An AI-configured fixture (fake local LLM) that ALSO turns on the background AI workers that are off by
// default — with a short poll interval — so their real end-to-end path can be exercised in a bounded
// wall-clock. Isolated in its own collection so the extra background load never disturbs the main
// ai-local collection's tests.
public sealed class AiWorkersFixture : AiLocalFixture
{
    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        base.ConfigureApp(psi);

        // Market-watch alert worker (AlertEvaluator): on, polling fast so a new rule is evaluated within
        // seconds instead of the 5-minute production cadence.
        psi.Environment["App__Alerts__Enabled"] = "true";
        psi.Environment["App__Alerts__PollInterval"] = "00:00:02";

        // Portfolio-agent worker (PortfolioAgentService): on, running fast so a Suggest-autonomy mandate
        // produces a proposal within seconds instead of the 30-minute production cadence. Suggest never
        // executes — it only records a proposal — so no orders/containers are touched.
        psi.Environment["App__Agent__Enabled"] = "true";
        psi.Environment["App__Agent__Interval"] = "00:00:02";
    }
}

[CollectionDefinition(Name)]
public sealed class AiWorkersCollection : ICollectionFixture<AiWorkersFixture>
{
    public const string Name = "ai-workers";
}
