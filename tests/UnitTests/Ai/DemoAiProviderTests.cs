using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Xunit;

namespace UnitTests.Ai;

public sealed class DemoAiProviderTests
{
    private static AiProviderRequest Request(string user = "review my bot", AiImage? image = null, bool web = false) =>
        new(AiProviderKind.Demo, "https://demo.local/", "cmind-demo", null, 4000,
            AiProviderCapabilities.DefaultFor(AiProviderKind.Demo), "sys", user, web, image);

    [Fact]
    public async Task Returns_canned_marked_response_without_network()
    {
        var result = await new DemoAiProvider().CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeTrue();
        result.Text.Should().Contain(DemoAiProvider.Marker);
        result.Text.Should().Contain("review my bot");
    }

    [Fact]
    public async Task Mentions_vision_and_web_search_when_requested()
    {
        var result = await new DemoAiProvider().CompleteAsync(
            Request(image: new AiImage("image/png", "x"), web: true), CancellationToken.None);
        result.Text.Should().Contain("image").And.Contain("Web search");
    }

    [Fact]
    public void Kind_is_demo() => new DemoAiProvider().Kind.Should().Be(AiProviderKind.Demo);
}
