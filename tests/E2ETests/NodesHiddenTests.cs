using System.Diagnostics;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

// White-label deployment that hides nodes entirely: NodesUi = Hidden. The nav link is gone and the /nodes
// route redirects away — the cluster is meant to be grown by auto-discovery only.
public sealed class NodesHiddenFixture : AppFixture
{
    protected override void ConfigureApp(ProcessStartInfo psi)
    {
        psi.Environment["App__Branding__NodesUi"] = "Hidden";
    }
}

[CollectionDefinition(Name)]
public sealed class NodesHiddenCollection : ICollectionFixture<NodesHiddenFixture>
{
    public const string Name = "nodes-hidden";
}

[Collection(NodesHiddenCollection.Name)]
public sealed class NodesHiddenTests(NodesHiddenFixture app)
{
    [Fact]
    public async Task Hidden_mode_drops_the_nav_link_and_redirects_the_page()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");

        (await page.Locator("a[href='/nodes']").CountAsync()).Should().Be(0);

        await page.GotoAsync("/nodes");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        // The page guard redirects a hidden Nodes surface back to the dashboard.
        await page.WaitForURLAsync(url => !url.EndsWith("/nodes"), new() { Timeout = 15000 });
        page.Url.Should().NotEndWith("/nodes");
    }

    [Fact]
    public async Task Hidden_mode_also_gates_the_node_api_not_just_the_ui()
    {
        // H-02: hiding the UI must hide the API too — a hidden surface must not be readable by URL.
        var page = await app.NewAuthedPageAsync();
        var res = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/nodes/");
        res.Status.Should().Be(404, "GET /api/nodes must 404 when NodesUi=Hidden (UI and API must agree)");
    }
}
