using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// Opt-in E2E for the shipped built-in local LLM (Microsoft.ML.OnnxRuntimeGenAI). Boots the app with the
// built-in enabled and pointed at a real ONNX GenAI model dir (AI_ONNX_MODEL). Skipped cleanly when the
// env is absent (the fixture does not even boot). Covers BOTH a UI drive and a non-UI API drive.
public sealed class OnnxAppFixture : AppFixture
{
    public string? ModelPath => Environment.GetEnvironmentVariable("AI_ONNX_MODEL");
    public bool Available => !string.IsNullOrWhiteSpace(ModelPath);
    protected override bool ShouldStart => Available;

    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        // Re-enable the built-in (the base fixture turns it off) and point it at the real model, so the
        // app seeds + activates the built-in ONNX provider on startup.
        psi.Environment["App__Ai__BuiltIn__Enabled"] = "true";
        psi.Environment["App__Ai__BuiltIn__ModelPath"] = ModelPath!;
    }
}

[CollectionDefinition(Name)]
public sealed class OnnxCollection : ICollectionFixture<OnnxAppFixture>
{
    public const string Name = "ai-onnx";
}

[Collection(OnnxCollection.Name)]
public sealed class OnnxE2ETests(OnnxAppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 60000 };

    [Fact]
    public async Task NonUi_built_in_is_active_and_a_feature_completes()
    {
        if (!app.Available) return; // set AI_ONNX_MODEL to run

        var page = await app.NewAuthedPageAsync();

        var status = await page.APIRequest.GetAsync("/api/ai/status");
        using var statusDoc = JsonDocument.Parse(await status.TextAsync());
        statusDoc.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
        statusDoc.RootElement.GetProperty("kind").GetString().Should().Be("BuiltInOnnx");

        var review = await page.APIRequest.PostAsync("/api/ai/review",
            new() { DataObject = new { Language = "CSharp", Source = "public class Bot {}" } });
        using var doc = JsonDocument.Parse(await review.TextAsync());
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("text").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Ui_review_renders_built_in_output()
    {
        if (!app.Available) return; // set AI_ONNX_MODEL to run

        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/ai/review", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForAppReadyAsync();

        await page.GetByLabel("cBot source").FillAsync("public class Bot { }");
        var button = page.Locator("button:has-text('Review cBot')");
        await Assertions.Expect(button).ToBeEnabledAsync(new() { Timeout = 20000 });
        await button.ClickAsync();

        var output = page.Locator("[data-testid=ai-output]");
        await Assertions.Expect(output).ToBeVisibleAsync(Slow);
        (await output.InnerTextAsync()).Should().NotBeNullOrWhiteSpace();
    }
}
