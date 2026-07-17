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
        await page.WaitForAppReadyAsync();
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
            await page.WaitForAppReadyAsync();
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
    public async Task Two_challenges_record_equity_independently_per_row()
    {
        var page = await app.NewAuthedPageAsync();

        var nameA = $"E2E-A {Guid.NewGuid():N}";
        var nameB = $"E2E-B {Guid.NewGuid():N}";
        var idA = await CreateChallengeAsync(page, nameA);
        var idB = await CreateChallengeAsync(page, nameB);

        try
        {
            await page.GotoAsync("/prop-firm");
            await page.WaitForAppReadyAsync();
            await Assertions.Expect(page.GetByText(nameA)).ToBeVisibleAsync(Slow);
            await Assertions.Expect(page.GetByText(nameB)).ToBeVisibleAsync(Slow);

            // Type a distinct value in each row's equity input, then record only row B.
            var rowA = page.Locator("tr", new() { HasTextString = nameA });
            var rowB = page.Locator("tr", new() { HasTextString = nameB });

            await rowA.Locator("input").First.FillAsync("105000");

            // Retry the fill + Record until the API reflects it: a fill/click dropped before the Blazor
            // circuit is interactive silently no-ops, and the old fixed 1s wait would then assert a stale
            // value. Re-recording the same value is idempotent; row A is never recorded so it stays put.
            var recordB = rowB.GetByRole(AriaRole.Button, new() { NameString = "Record" });
            for (var attempt = 0; attempt < 20; attempt++)
            {
                await rowB.Locator("input").First.FillAsync("111000");
                await recordB.ClickAsync();
                var probe = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/prop-firm/challenges/{idB}");
                if (JsonDocument.Parse(await probe.TextAsync()).RootElement.GetProperty("currentEquity").GetDecimal() == 111000m)
                    break;
                await page.WaitForTimeoutAsync(500);
            }

            // Only challenge B's equity changed; challenge A stays at its starting balance.
            var detailA = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/prop-firm/challenges/{idA}");
            var detailB = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/prop-firm/challenges/{idB}");
            JsonDocument.Parse(await detailB.TextAsync()).RootElement.GetProperty("currentEquity").GetDecimal()
                .Should().Be(111000m, "row B's own input value was recorded");
            JsonDocument.Parse(await detailA.TextAsync()).RootElement.GetProperty("currentEquity").GetDecimal()
                .Should().Be(100000m, "recording row B must not touch row A");
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop-firm/challenges/{idA}");
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop-firm/challenges/{idB}");
        }
    }

    [Fact]
    public async Task Failed_challenge_shows_breach_cause_and_detail_dialog()
    {
        var page = await app.NewAuthedPageAsync();
        var name = $"E2E-fail {Guid.NewGuid():N}";
        var id = await CreateChallengeAsync(page, name, profitTarget: 50.0);

        try
        {
            var breach = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}/equity",
                new APIRequestContextOptions { DataObject = new { Equity = 94000m } });
            breach.Status.Should().Be(200);

            await page.GotoAsync("/prop-firm");
            await page.WaitForAppReadyAsync();
            await Assertions.Expect(page.GetByText(name)).ToBeVisibleAsync(Slow);

            // D-03: the breach cause is visible in the table for a failed challenge.
            await Assertions.Expect(page.Locator("[data-testid=challenge-breach]").First).ToBeVisibleAsync(Slow);
            await Assertions.Expect(page.GetByText("DailyLoss").First).ToBeVisibleAsync(Slow);

            // D-04: the view/eye icon opens a detail dialog that renders for a terminal challenge without crashing.
            var row = page.Locator("tr", new() { HasTextString = name });
            await row.GetByLabel("View challenge").ClickAsync();
            await Assertions.Expect(page.Locator("[data-testid=challenge-detail]")).ToBeVisibleAsync(Slow);
            await Assertions.Expect(page.Locator("[data-testid=challenge-detail-breach]")).ToBeVisibleAsync(Slow);
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}");
        }
    }

    [Fact]
    public async Task Delete_challenge_asks_for_confirmation_first()
    {
        var page = await app.NewAuthedPageAsync();
        var name = $"E2E-del {Guid.NewGuid():N}";
        var id = await CreateChallengeAsync(page, name);
        await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}/stop");

        try
        {
            await page.GotoAsync("/prop-firm");
            await page.WaitForAppReadyAsync();
            await Assertions.Expect(page.GetByText(name)).ToBeVisibleAsync(Slow);

            var row = page.Locator("tr", new() { HasTextString = name });
            await row.GetByLabel("Delete challenge").ClickAsync();

            // D-05/D-06 pattern: destructive delete opens a confirm dialog before the DELETE fires.
            await Assertions.Expect(page.Locator("[data-testid=confirm-accept]")).ToBeVisibleAsync(Slow);
            // Cancel — the challenge must still exist.
            await page.ClickAsync("[data-testid=confirm-cancel]");
            var still = await page.APIRequest.GetAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}");
            still.Status.Should().Be(200, "cancelling the confirm dialog must not delete the challenge");
        }
        finally
        {
            await page.APIRequest.DeleteAsync($"{app.BaseUrl}/api/prop-firm/challenges/{id}");
        }
    }

    private async Task<Guid> CreateChallengeAsync(IPage page, string name, double profitTarget = 10.0)
    {
        var create = await page.APIRequest.PostAsync($"{app.BaseUrl}/api/prop-firm/challenges",
            new APIRequestContextOptions
            {
                DataObject = new
                {
                    Name = name,
                    TradingAccountId = Guid.NewGuid(),
                    StartingBalance = 100000m,
                    ProfitTargetPercent = profitTarget,
                    MaxDailyLossPercent = 5.0,
                    MaxTotalDrawdownPercent = 10.0,
                    DrawdownMode = 0,
                    MinTradingDays = 0,
                    SingleStep = true
                }
            });
        create.Status.Should().Be(200);
        return JsonDocument.Parse(await create.TextAsync()).RootElement.GetProperty("id").GetGuid();
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
