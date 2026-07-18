using System.Net;
using System.Text;
using Core.Ai;
using Core.Options;
using FluentAssertions;
using Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace UnitTests.Ai;

public sealed class AiModelCatalogTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static AiModelCatalog Catalog(
        Func<HttpRequestMessage, HttpResponseMessage> respond, AppOptions? options = null)
    {
        var monitor = Substitute.For<IOptionsMonitor<AppOptions>>();
        monitor.CurrentValue.Returns(options ?? new AppOptions());
        return new AiModelCatalog(
            new HttpClient(new StubHandler(respond)), monitor, NullLogger<AiModelCatalog>.Instance);
    }

    [Fact]
    public async Task OpenAiCompatible_lists_model_ids_from_the_data_array()
    {
        var catalog = Catalog(req =>
        {
            req.Method.Should().Be(HttpMethod.Get);
            req.RequestUri!.AbsolutePath.Should().EndWith("/models");
            return Json("""{"object":"list","data":[{"id":"llama-3.1-8b"},{"id":"qwen2.5-coder"}]}""");
        });

        var models = await catalog.ListModelsAsync(
            AiProviderKind.OpenAiCompatible, new AiEndpoint("http://127.0.0.1:1234/v1/"), null, default);

        models.Select(m => m.Id).Should().Equal("llama-3.1-8b", "qwen2.5-coder");
    }

    [Fact]
    public async Task OpenAiCompatible_sends_bearer_key_when_present()
    {
        string? auth = null;
        var catalog = Catalog(req =>
        {
            auth = req.Headers.TryGetValues("Authorization", out var v) ? v.First() : null;
            return Json("""{"data":[{"id":"m"}]}""");
        });

        await catalog.ListModelsAsync(
            AiProviderKind.OpenAiCompatible, new AiEndpoint("http://127.0.0.1:1234/v1/"), "secret", default);

        auth.Should().Be("Bearer secret");
    }

    [Fact]
    public async Task Non_success_status_degrades_to_empty_list()
    {
        var catalog = Catalog(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var models = await catalog.ListModelsAsync(
            AiProviderKind.OpenAiCompatible, new AiEndpoint("http://127.0.0.1:1234/v1/"), null, default);

        models.Should().BeEmpty();
    }

    [Fact]
    public async Task Malformed_body_degrades_to_empty_list()
    {
        var catalog = Catalog(_ => Json("not json"));

        var models = await catalog.ListModelsAsync(
            AiProviderKind.OpenAiCompatible, new AiEndpoint("http://127.0.0.1:1234/v1/"), null, default);

        models.Should().BeEmpty();
    }

    [Fact]
    public async Task Gemini_strips_the_models_prefix_from_ids()
    {
        var catalog = Catalog(_ => Json("""{"models":[{"name":"models/gemini-1.5-pro"},{"name":"gemini-flash"}]}"""));

        var models = await catalog.ListModelsAsync(
            AiProviderKind.Gemini, new AiEndpoint("https://generativelanguage.googleapis.com/"), "key", default);

        models.Select(m => m.Id).Should().Equal("gemini-1.5-pro", "gemini-flash");
    }

    [Fact]
    public async Task Anthropic_without_key_returns_empty_without_calling_the_endpoint()
    {
        var called = false;
        var catalog = Catalog(_ => { called = true; return Json("{}"); });

        var models = await catalog.ListModelsAsync(
            AiProviderKind.Anthropic, new AiEndpoint("https://api.anthropic.com/"), null, default);

        models.Should().BeEmpty();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task AzureOpenAi_has_no_discoverable_list_and_returns_empty()
    {
        var catalog = Catalog(_ => Json("""{"data":[{"id":"x"}]}"""));

        var models = await catalog.ListModelsAsync(
            AiProviderKind.AzureOpenAi, new AiEndpoint("https://x.openai.azure.com/"), "key", default);

        models.Should().BeEmpty();
    }

    [Fact]
    public async Task BuiltInOnnx_enumerates_installed_model_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), "onnx-catalog-" + Guid.NewGuid().ToString("N"));
        var modelA = Path.Combine(root, "phi-3.5-mini");
        Directory.CreateDirectory(modelA);
        await File.WriteAllTextAsync(Path.Combine(modelA, "genai_config.json"), "{}");
        Directory.CreateDirectory(Path.Combine(root, "empty-not-a-model"));

        try
        {
            var options = new AppOptions { Ai = new AiOptions { BuiltIn = new AiBuiltInOptions { ModelPath = root } } };
            var catalog = Catalog(_ => Json("{}"), options);

            var models = await catalog.ListModelsAsync(
                AiProviderKind.BuiltInOnnx, new AiEndpoint("https://builtin.local/"), null, default);

            models.Select(m => m.Id).Should().ContainSingle().Which.Should().Be("phi-3.5-mini");
            models[0].Family.Should().Be("ONNX");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best effort */ }
        }
    }
}
