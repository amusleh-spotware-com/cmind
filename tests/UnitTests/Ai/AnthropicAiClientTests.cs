using System.Net;
using Core.Ai;
using Core.Constants;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace UnitTests.Ai;

public sealed class AnthropicAiClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }

    private const string ValidBody = """{"content":[{"type":"text","text":"hello"}]}""";
    private const string EmptyBody = """{"content":[]}""";

    private static AnthropicAiClient Create(HttpStatusCode status, string body, string? key = "sk-test")
    {
        var http = new HttpClient(new StubHandler(status, body));
        var options = Substitute.For<IOptionsMonitor<AppOptions>>();
        options.CurrentValue.Returns(new AppOptions { Ai = new AiOptions { BaseUrl = AiConstants.DefaultBaseUrl } });
        var keyStore = Substitute.For<IAiKeyStore>();
        keyStore.CurrentKey.Returns(key);
        keyStore.HasKey.Returns(key is not null);
        return new AnthropicAiClient(http, options, keyStore, NullLogger<AnthropicAiClient>.Instance);
    }

    private static AiTextRequest Request() => new("system", "user");

    [Fact]
    public async Task Returns_disabled_when_no_key()
    {
        var client = Create(HttpStatusCode.OK, ValidBody, key: null);
        var result = await client.CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Be(AiConstants.DisabledMessage);
    }

    [Fact]
    public async Task Succeeds_on_valid_response()
    {
        var client = Create(HttpStatusCode.OK, ValidBody);
        var result = await client.CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeTrue();
        result.Text.Should().Be("hello");
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task Fails_gracefully_on_error_status(HttpStatusCode status)
    {
        var client = Create(status, """{"error":"nope"}""");
        var result = await client.CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain(((int)status).ToString());
    }

    [Fact]
    public async Task Fails_gracefully_on_malformed_json()
    {
        var client = Create(HttpStatusCode.OK, "this is not json");
        var result = await client.CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Fails_gracefully_on_empty_content()
    {
        var client = Create(HttpStatusCode.OK, EmptyBody);
        var result = await client.CompleteAsync(Request(), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("empty");
    }
}
