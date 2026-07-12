using System.Net;
using System.Text.Json;
using Core.Ai;
using FluentAssertions;
using Infrastructure.Ai.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiProviderAdapterTests
{
    // Captures the outgoing request (URI, headers, body) and returns a canned response, so each adapter's
    // request translation AND response parse can be asserted without any network.
    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    private static AiProviderRequest Request(
        AiProviderKind kind, string baseUrl, string? key = "sk-test", bool webSearch = false,
        AiImage? image = null, bool systemRole = true) =>
        new(kind, baseUrl, "the-model", key, 1234,
            new AiProviderCapabilities(SupportsWebSearch: true, SupportsVision: true, SupportsSystemRole: systemRole, SupportsTools: true),
            System: "SYS", User: "USER", EnableWebSearch: webSearch, Image: image);

    // ---------------- Anthropic ----------------

    [Fact]
    public async Task Anthropic_emits_messages_wire_and_parses_content_text()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"content":[{"type":"text","text":"hello"}]}""");
        var provider = new AnthropicAiProvider(new HttpClient(handler), NullLogger<AnthropicAiProvider>.Instance);

        var result = await provider.CompleteAsync(Request(AiProviderKind.Anthropic, "https://api.anthropic.com/", webSearch: true), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("hello");
        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/v1/messages");
        handler.Request.Headers.Contains("x-api-key").Should().BeTrue();
        using var doc = JsonDocument.Parse(handler.RequestBody!);
        doc.RootElement.GetProperty("model").GetString().Should().Be("the-model");
        doc.RootElement.GetProperty("max_tokens").GetInt32().Should().Be(1234);
        doc.RootElement.GetProperty("system").GetString().Should().Be("SYS");
        doc.RootElement.GetProperty("tools").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Anthropic_fails_gracefully_on_error_status()
    {
        var handler = new CapturingHandler(HttpStatusCode.InternalServerError, """{"error":"nope"}""");
        var provider = new AnthropicAiProvider(new HttpClient(handler), NullLogger<AnthropicAiProvider>.Instance);
        var result = await provider.CompleteAsync(Request(AiProviderKind.Anthropic, "https://api.anthropic.com/"), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("500");
    }

    // ---------------- OpenAI-compatible (covers every local runtime) ----------------

    [Fact]
    public async Task OpenAi_emits_chat_completions_and_parses_choices_message_content()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"hi there"}}]}""");
        var provider = new OpenAiCompatibleProvider(new HttpClient(handler), NullLogger<OpenAiCompatibleProvider>.Instance);

        var result = await provider.CompleteAsync(Request(AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("hi there");
        handler.Request!.RequestUri!.AbsolutePath.Should().Be("/v1/chat/completions");
        handler.Request.Headers.Authorization!.ToString().Should().Be("Bearer sk-test");
        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var messages = doc.RootElement.GetProperty("messages");
        messages[0].GetProperty("role").GetString().Should().Be("system");
        messages[1].GetProperty("role").GetString().Should().Be("user");
    }

    [Fact]
    public async Task OpenAi_omits_authorization_when_key_null_for_local_endpoint()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");
        var provider = new OpenAiCompatibleProvider(new HttpClient(handler), NullLogger<OpenAiCompatibleProvider>.Instance);

        await provider.CompleteAsync(Request(AiProviderKind.OpenAiCompatible, "http://localhost:11434/v1/", key: null), CancellationToken.None);

        handler.Request!.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task OpenAi_folds_system_into_user_when_system_role_unsupported()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");
        var provider = new OpenAiCompatibleProvider(new HttpClient(handler), NullLogger<OpenAiCompatibleProvider>.Instance);

        await provider.CompleteAsync(
            Request(AiProviderKind.OpenAiCompatible, "http://localhost:1234/v1/", key: null, systemRole: false), CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.RequestBody!);
        var messages = doc.RootElement.GetProperty("messages");
        messages.GetArrayLength().Should().Be(1);
        messages[0].GetProperty("role").GetString().Should().Be("user");
        messages[0].GetProperty("content").GetString().Should().Contain("SYS").And.Contain("USER");
    }

    [Fact]
    public async Task OpenAi_maps_image_to_image_url_part()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""");
        var provider = new OpenAiCompatibleProvider(new HttpClient(handler), NullLogger<OpenAiCompatibleProvider>.Instance);

        await provider.CompleteAsync(
            Request(AiProviderKind.OpenAiCompatible, "https://api.openai.com/v1/", image: new AiImage("image/png", "BASE64")),
            CancellationToken.None);

        handler.RequestBody!.Should().Contain("image_url").And.Contain("data:image/png;base64,BASE64");
    }

    // ---------------- Gemini ----------------

    [Fact]
    public async Task Gemini_emits_generate_content_and_parses_candidates()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK,
            """{"candidates":[{"content":{"parts":[{"text":"grounded answer"}]}}]}""");
        var provider = new GeminiAiProvider(new HttpClient(handler), NullLogger<GeminiAiProvider>.Instance);

        var result = await provider.CompleteAsync(
            Request(AiProviderKind.Gemini, "https://generativelanguage.googleapis.com/", webSearch: true), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Text.Should().Be("grounded answer");
        handler.Request!.RequestUri!.AbsolutePath.Should().Contain(":generateContent");
        handler.Request.RequestUri.Query.Should().Contain("key=sk-test");
        handler.RequestBody!.Should().Contain("systemInstruction").And.Contain("google_search");
    }
}
