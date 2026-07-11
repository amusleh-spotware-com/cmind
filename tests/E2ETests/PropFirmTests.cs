using System.Text.Json;
using FluentAssertions;
using Microsoft.Playwright;
using Xunit;

namespace E2ETests;

[Collection(AppCollection.Name)]
public sealed class PropFirmTests(AppFixture app)
{
    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 15000 };

    [Fact]
    public async Task Challenge_lifecycle_renders_and_passes_on_target()
    {
        var page = await app.NewAuthedPageAsync();
        await page.GotoAsync("/prop-firm");
        await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Assertions.Expect(page.GetByText("New Challenge")).ToBeVisibleAsync(Slow);

        var name = $"E2E {Guid.NewGuid():N}";
        var create = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    Name = name,
                    TradingAccountId = Guid.NewGuid(),
                    StartingBalance = 100000m,
                    ProfitTargetPercent = 10.0,
                    MaxDailyLossPercent = 5.0,
                    MaxTotalDrawdownPercent = 10.0,
                    DrawdownMode = 0,
                    MinTradingDays = 0,
                    SingleStep = true
                }
            });
        create.Status.Should().Be(200);
        var id = JsonDocument.Parse(await create.TextAsync()).RootElement.GetProperty("id").GetGuid();

        try
        {
            await page.GotoAsync("/prop-firm");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await Assertions.Expect(page.GetByText(name)).ToBeVisibleAsync(Slow);

            var equity = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}/equity",
                new APIRequestContextOptions { DataObject = new { Equity = 110000m } });
            equity.Status.Should().Be(200);
            JsonDocument.Parse(await equity.TextAsync()).RootElement.GetProperty("status").GetString()
                .Should().Be("Passed");
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}");
        }
    }

    [Fact]
    public async Task Challenge_stop_start_and_breach_flow()
    {
        var page = await app.NewAuthedPageAsync();

        var create = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    Name = $"E2E-breach {Guid.NewGuid():N}",
                    TradingAccountId = Guid.NewGuid(),
                    StartingBalance = 100000m,
                    ProfitTargetPercent = 50.0,
                    MaxDailyLossPercent = 5.0,
                    MaxTotalDrawdownPercent = 10.0,
                    DrawdownMode = 0,
                    MinTradingDays = 0,
                    SingleStep = true,
                    Kind = 0,
                    DailyLossBasis = 0
                }
            });
        create.Status.Should().Be(200);
        var id = JsonDocument.Parse(await create.TextAsync()).RootElement.GetProperty("id").GetGuid();

        try
        {
            var stop = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}/stop");
            stop.Status.Should().Be(200);
            JsonDocument.Parse(await stop.TextAsync()).RootElement.GetProperty("status").GetString()
                .Should().Be("Stopped");

            var start = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}/start");
            start.Status.Should().Be(200);
            JsonDocument.Parse(await start.TextAsync()).RootElement.GetProperty("status").GetString()
                .Should().Be("Active");

            var breach = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}/equity",
                new APIRequestContextOptions { DataObject = new { Equity = 94000m } });
            breach.Status.Should().Be(200);
            var root = JsonDocument.Parse(await breach.TextAsync()).RootElement;
            root.GetProperty("status").GetString().Should().Be("Failed");
            root.GetProperty("breach").GetString().Should().Be("DailyLoss");
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}");
        }
    }

    [Fact]
    public async Task Templates_endpoint_returns_industry_presets()
    {
        var page = await app.NewAuthedPageAsync();
        var res = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/prop-firm/templates");
        res.Status.Should().Be(200);
        var kinds = JsonDocument.Parse(await res.TextAsync()).RootElement
            .EnumerateArray().Select(e => e.GetProperty("kind").GetString()).ToList();
        kinds.Should().Contain(["OnePhase", "TwoPhase", "ThreePhase", "InstantFunding"]);
    }
}
