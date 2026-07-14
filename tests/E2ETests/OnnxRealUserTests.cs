using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Real-model verification through the app's shipped built-in ONNX provider
// (Microsoft.ML.OnnxRuntimeGenAI) — an actual model running in-process, no external LLM server. Extends
// the review-only OnnxE2ETests with a second data-independent feature so a green run proves a real model
// answers through the full stack, not only the canned fake. Reuses OnnxAppFixture, so the whole class
// skips cleanly when AI_ONNX_MODEL is absent (the CI PR lane); locally, set AI_ONNX_MODEL to run.
//
// Assertions are non-deterministic-safe: output present + non-empty + no error UI — never a canned marker.
//
// Feature choice is deliberate: on a CPU-only Phi-3 the wall-clock is dominated by tokens emitted, so we
// cover the *bounded-output* features (review — in OnnxE2ETests — and debate, capped at DebateMaxTokens
// with a trivial source that the model completes quickly). Unbounded features (codegen, sentiment) can run
// for minutes on CPU and are proven deterministically through the fake LLM lane instead. The visibility
// budget is generous so a slow-but-correct real inference passes on machine speed rather than racing it.
[Collection(OnnxCollection.Name)]
public sealed class OnnxRealUserTests(OnnxAppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 180000 };

    private async Task<IPage> OpenAsync(string route)
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync(route, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        return page;
    }

    // Debate, driven as a user against the real ONNX model — bounded output, so it completes on CPU.
    [Fact]
    public async Task Ui_debate_renders_built_in_output()
    {
        if (!app.Available) return; // set AI_ONNX_MODEL to run

        var page = await OpenAsync("/ai/debate");
        await page.GetByLabel("cBot source").FillAsync("public class Bot { }");
        var button = page.Locator("button:has-text('Run the debate')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();

        var output = page.Locator("[data-testid=ai-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(Slow);
        (await output.InnerTextAsync()).Should().NotBeNullOrWhiteSpace();
        (await page.Locator(".blazor-error-ui").IsVisibleAsync()).Should().BeFalse();
    }
}
