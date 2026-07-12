using System.Diagnostics;
using Xunit;

namespace E2ETests;

// An app fixture with AI configured, so AI features can be driven end-to-end through the real UI.
//
// Provider selection (per the "real creds if provided, else fake LLM" rule):
//   • If AI_E2E_BASEURL (and optionally AI_E2E_API_KEY / AI_E2E_KIND / AI_E2E_MODEL) is set, the app
//     is pointed at that REAL provider and tests assert only that output renders (non-deterministic).
//   • Otherwise an in-process fake OpenAI-compatible endpoint (FakeLocalLlmServer) is booted and the app
//     is pointed at it, so every AI feature returns CannedReply — fully deterministic, zero external deps.
//
// The fake speaks the OpenAI wire, so this same fixture validates every OpenAI-compatible target (all
// local runtimes + most clouds) without change — the "works with any provider, no diff" property.
public sealed class AiLocalFixture : AppFixture
{
    public const string CannedReply = "E2E-LOCAL-LLM-REPLY";

    private FakeLocalLlmServer? _llm;

    public bool UsingFakeLlm { get; private set; }

    protected override Task OnBeforeStartAsync()
    {
        var realBaseUrl = Environment.GetEnvironmentVariable("AI_E2E_BASEURL");
        if (string.IsNullOrWhiteSpace(realBaseUrl))
        {
            _llm = new FakeLocalLlmServer(CannedReply);
            UsingFakeLlm = true;
        }
        return Task.CompletedTask;
    }

    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        // Seed one active provider via config (App:Ai:Providers[0]); the store imports it on startup.
        if (UsingFakeLlm)
        {
            psi.Environment["App__Ai__ActiveProvider"] = "OpenAiCompatible";
            psi.Environment["App__Ai__Providers__0__Kind"] = "OpenAiCompatible";
            psi.Environment["App__Ai__Providers__0__BaseUrl"] = _llm!.BaseUrl;
            psi.Environment["App__Ai__Providers__0__Model"] = "fake-local-model";
        }
        else
        {
            var kind = Environment.GetEnvironmentVariable("AI_E2E_KIND") ?? "OpenAiCompatible";
            psi.Environment["App__Ai__ActiveProvider"] = kind;
            psi.Environment["App__Ai__Providers__0__Kind"] = kind;
            psi.Environment["App__Ai__Providers__0__BaseUrl"] = Environment.GetEnvironmentVariable("AI_E2E_BASEURL")!;
            psi.Environment["App__Ai__Providers__0__Model"] =
                Environment.GetEnvironmentVariable("AI_E2E_MODEL") ?? "gpt-4o";
            var key = Environment.GetEnvironmentVariable("AI_E2E_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) psi.Environment["App__Ai__Providers__0__ApiKey"] = key;
        }
    }

    protected override Task OnDisposeAsync()
    {
        _llm?.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class AiLocalCollection : ICollectionFixture<AiLocalFixture>
{
    public const string Name = "ai-local";
}
