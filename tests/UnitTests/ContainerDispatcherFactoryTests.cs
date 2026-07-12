using System.Net.Http;
using Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Core.Options;
using Nodes;
using NSubstitute;
using Xunit;

namespace UnitTests;

public class ContainerDispatcherFactoryTests
{
    private static HttpContainerDispatcher CreateHttp() =>
        new(Substitute.For<IHttpClientFactory>(), Substitute.For<ISecretProtector>(), TimeProvider.System);

    [Fact]
    public void For_LocalNode_returns_local_dispatcher()
    {
        var http = CreateHttp();
        var opts = new TestMonitor(new AppOptions());
        var local = new LocalContainerDispatcher(Substitute.For<ISecretProtector>(), opts,
            NullLogger<LocalContainerDispatcher>.Instance, TimeProvider.System);
        var factory = new ContainerDispatcherFactory(http, local);

        factory.For(new LocalNode()).Should().BeSameAs(local);
    }

    [Fact]
    public void For_CtraderCliNode_returns_http_dispatcher()
    {
        var http = CreateHttp();
        var opts = new TestMonitor(new AppOptions());
        var local = new LocalContainerDispatcher(Substitute.For<ISecretProtector>(), opts,
            NullLogger<LocalContainerDispatcher>.Instance, TimeProvider.System);
        var factory = new ContainerDispatcherFactory(http, local);

        factory.For(new ActiveMixedNode()).Should().BeSameAs(http);
    }

    private sealed class TestMonitor(AppOptions value) : IOptionsMonitor<AppOptions>
    {
        public AppOptions CurrentValue { get; } = value;
        public AppOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AppOptions, string?> listener) => null;
    }
}
