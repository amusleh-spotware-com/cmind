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
    [Fact]
    public void For_LocalNode_returns_local_dispatcher()
    {
        var ssh = new SshContainerDispatcher(Substitute.For<ISecretProtector>(), NullLogger<SshContainerDispatcher>.Instance);
        var opts = new TestMonitor(new AppOptions());
        var local = new LocalContainerDispatcher(Substitute.For<ISecretProtector>(), opts, NullLogger<LocalContainerDispatcher>.Instance);
        var factory = new ContainerDispatcherFactory(ssh, local);

        factory.For(new LocalNode()).Should().BeSameAs(local);
    }

    [Fact]
    public void For_RemoteNode_returns_ssh_dispatcher()
    {
        var ssh = new SshContainerDispatcher(Substitute.For<ISecretProtector>(), NullLogger<SshContainerDispatcher>.Instance);
        var opts = new TestMonitor(new AppOptions());
        var local = new LocalContainerDispatcher(Substitute.For<ISecretProtector>(), opts, NullLogger<LocalContainerDispatcher>.Instance);
        var factory = new ContainerDispatcherFactory(ssh, local);

        factory.For(new ActiveMixedNode()).Should().BeSameAs(ssh);
    }

    private sealed class TestMonitor(AppOptions value) : IOptionsMonitor<AppOptions>
    {
        public AppOptions CurrentValue { get; } = value;
        public AppOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AppOptions, string?> listener) => null;
    }
}
