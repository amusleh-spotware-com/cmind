using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai;
using NSubstitute;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiFeatureServiceTests
{
    private static IAiClient Client(AiResult result, bool enabled = true)
    {
        var client = Substitute.For<IAiClient>();
        client.Enabled.Returns(enabled);
        client.CompleteAsync(Arg.Any<AiTextRequest>(), Arg.Any<CancellationToken>()).Returns(result);
        return client;
    }

    [Fact]
    public async Task GenerateCBot_passes_client_result_through()
    {
        var service = new AiFeatureService(Client(AiResult.Ok("source")));
        var result = await service.GenerateCBotAsync("CSharp", "rsi bot", default);
        result.Success.Should().BeTrue();
        result.Text.Should().Be("source");
    }

    [Fact]
    public async Task MarketSentiment_enables_web_search()
    {
        var client = Client(AiResult.Ok("bullish"));
        var service = new AiFeatureService(client);
        await service.MarketSentimentAsync("EURUSD", default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.EnableWebSearch), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VisionToStrategy_attaches_image()
    {
        var client = Client(AiResult.Ok("plan"));
        var service = new AiFeatureService(client);
        await service.VisionToStrategyAsync(new AiImage("image/png", "abc"), null, default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.Image != null && r.Image.Base64Data == "abc"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FixCBot_passes_client_result_through()
    {
        var service = new AiFeatureService(Client(AiResult.Ok("fixed source")));
        var result = await service.FixCBotAsync("CSharp", "bad source", "error CS0103", default);
        result.Success.Should().BeTrue();
        result.Text.Should().Be("fixed source");
    }

    [Fact]
    public async Task ProposeParamSetSuite_passes_client_result_through()
    {
        var service = new AiFeatureService(Client(AiResult.Ok("[]")));
        var result = await service.ProposeParamSetSuiteAsync("Bot", "{}", 3, default);
        result.Success.Should().BeTrue();
        result.Text.Should().Be("[]");
    }

    [Fact]
    public async Task ProposeAgentAction_forwards_max_tokens_and_disables_web_search()
    {
        var client = Client(AiResult.Ok("{}"));
        var service = new AiFeatureService(client);
        await service.ProposeAgentActionAsync("Bot", "grow safely", "{}", null, 1500, default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.MaxTokens == 1500 && !r.EnableWebSearch), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssessRiskActions_numbers_bots_and_caps_tokens()
    {
        var client = Client(AiResult.Ok("[]"));
        var service = new AiFeatureService(client);
        var running = new[]
        {
            new AiInstanceContext("BotA", "Run", "Running", "EURUSD", "h1", null),
            new AiInstanceContext("BotB", "Run", "Running", "GBPUSD", "m5", null)
        };
        await service.AssessRiskActionsAsync(running, 1000, default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.MaxTokens == 1000 && r.User.Contains("[0]") && r.User.Contains("[1]")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssessSymbolAlert_enables_web_search_and_caps_tokens()
    {
        var client = Client(AiResult.Ok("{\"alert\":false}"));
        var service = new AiFeatureService(client);
        await service.AssessSymbolAlertAsync("EURUSD", 1200, default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.EnableWebSearch && r.MaxTokens == 1200), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssessStrategyDecay_includes_both_reports_and_caps_tokens()
    {
        var client = Client(AiResult.Ok("stable"));
        var service = new AiFeatureService(client);
        await service.AssessStrategyDecayAsync("Bot", "PREVREPORT", "LATESTREPORT", "{}", 1500, default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.MaxTokens == 1500 && r.User.Contains("PREVREPORT") && r.User.Contains("LATESTREPORT")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PortfolioDigest_lists_every_bot_and_caps_tokens()
    {
        var client = Client(AiResult.Ok("digest"));
        var service = new AiFeatureService(client);
        var portfolio = new[]
        {
            new AiInstanceContext("Alpha", "Run", "Running", "EURUSD", "h1", null),
            new AiInstanceContext("Beta", "Backtest", "Completed", "GBPUSD", "m5", "done")
        };
        await service.PortfolioDigestAsync(portfolio, 2000, default);
        await client.Received().CompleteAsync(
            Arg.Is<AiTextRequest>(r => r.MaxTokens == 2000 && r.User.Contains("Alpha") && r.User.Contains("Beta")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Enabled_reflects_underlying_client()
    {
        new AiFeatureService(Client(AiResult.Ok(""), enabled: false)).Enabled.Should().BeFalse();
        new AiFeatureService(Client(AiResult.Ok(""), enabled: true)).Enabled.Should().BeTrue();
    }
}
