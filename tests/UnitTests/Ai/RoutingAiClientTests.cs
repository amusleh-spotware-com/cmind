using Core.Ai;
using Core.Constants;
using FluentAssertions;
using Infrastructure.Ai;
using Xunit;

namespace UnitTests.Ai;

public sealed class RoutingAiClientTests
{
    private sealed class FakeStore(ActiveAiProvider? active) : IAiProviderStore
    {
        public bool HasActive => active is not null;
        public ActiveAiProvider? Active => active;
        public Task<IReadOnlyList<AiProviderView>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiProviderView>>([]);
        public Task<Guid> UpsertAsync(UpsertAiProviderCommand command, CancellationToken ct) => Task.FromResult(Guid.NewGuid());
        public Task ActivateAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<AiProviderView>> ListForUserAsync(Core.UserId user, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AiProviderView>>([]);
        public Task<Guid> UpsertForUserAsync(Core.UserId user, UpsertAiProviderCommand command, CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());
        public Task ActivateForUserAsync(Core.UserId user, Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveForUserAsync(Core.UserId user, Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task SeedFromConfigAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CapturingProvider(AiProviderKind kind) : IAiProvider
    {
        public AiProviderKind Kind => kind;
        public AiProviderRequest? Seen { get; private set; }
        public Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
        {
            Seen = request;
            return Task.FromResult(AiResult.Ok($"from {kind}"));
        }
    }

    private static ActiveAiProvider ActiveOf(AiProviderKind kind, AiProviderCapabilities caps) =>
        new(kind, "https://host/", "model-x", "key", caps, 4000);

    [Fact]
    public async Task Disabled_when_no_active_provider()
    {
        var client = new RoutingAiClient(new FakeStore(null), []);
        client.Enabled.Should().BeFalse();
        var result = await client.CompleteAsync(new AiTextRequest("s", "u"), CancellationToken.None);
        result.Success.Should().BeFalse();
        result.Error.Should().Be(AiConstants.DisabledMessage);
    }

    [Fact]
    public async Task Routes_to_the_adapter_matching_the_active_kind()
    {
        var anthropic = new CapturingProvider(AiProviderKind.Anthropic);
        var gemini = new CapturingProvider(AiProviderKind.Gemini);
        var caps = AiProviderCapabilities.DefaultFor(AiProviderKind.Gemini);
        var client = new RoutingAiClient(new FakeStore(ActiveOf(AiProviderKind.Gemini, caps)), [anthropic, gemini]);

        var result = await client.CompleteAsync(new AiTextRequest("s", "u"), CancellationToken.None);

        result.Text.Should().Be("from Gemini");
        gemini.Seen.Should().NotBeNull();
        anthropic.Seen.Should().BeNull();
    }

    [Fact]
    public async Task Drops_web_search_when_capability_off()
    {
        var provider = new CapturingProvider(AiProviderKind.OpenAiCompatible);
        var caps = new AiProviderCapabilities(SupportsWebSearch: false, SupportsVision: true, SupportsSystemRole: true, SupportsTools: false);
        var client = new RoutingAiClient(new FakeStore(ActiveOf(AiProviderKind.OpenAiCompatible, caps)), [provider]);

        await client.CompleteAsync(new AiTextRequest("s", "u", EnableWebSearch: true), CancellationToken.None);

        provider.Seen!.EnableWebSearch.Should().BeFalse();
    }

    [Fact]
    public async Task Keeps_web_search_when_capability_on()
    {
        var provider = new CapturingProvider(AiProviderKind.Anthropic);
        var caps = new AiProviderCapabilities(SupportsWebSearch: true, SupportsVision: true, SupportsSystemRole: true, SupportsTools: true);
        var client = new RoutingAiClient(new FakeStore(ActiveOf(AiProviderKind.Anthropic, caps)), [provider]);

        await client.CompleteAsync(new AiTextRequest("s", "u", EnableWebSearch: true), CancellationToken.None);

        provider.Seen!.EnableWebSearch.Should().BeTrue();
    }

    [Fact]
    public async Task Vision_returns_typed_failure_when_unsupported_and_never_calls_adapter()
    {
        var provider = new CapturingProvider(AiProviderKind.OpenAiCompatible);
        var caps = new AiProviderCapabilities(SupportsWebSearch: false, SupportsVision: false, SupportsSystemRole: true, SupportsTools: false);
        var client = new RoutingAiClient(new FakeStore(ActiveOf(AiProviderKind.OpenAiCompatible, caps)), [provider]);

        var result = await client.CompleteAsync(
            new AiTextRequest("s", "u", Image: new AiImage("image/png", "x")), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be(AiConstants.VisionUnsupportedMessage);
        provider.Seen.Should().BeNull();
    }

    [Fact]
    public async Task Uses_active_max_tokens_when_request_omits_it()
    {
        var provider = new CapturingProvider(AiProviderKind.Anthropic);
        var caps = AiProviderCapabilities.DefaultFor(AiProviderKind.Anthropic);
        var client = new RoutingAiClient(new FakeStore(ActiveOf(AiProviderKind.Anthropic, caps)), [provider]);

        await client.CompleteAsync(new AiTextRequest("s", "u"), CancellationToken.None);

        provider.Seen!.MaxTokens.Should().Be(4000);
    }
}
