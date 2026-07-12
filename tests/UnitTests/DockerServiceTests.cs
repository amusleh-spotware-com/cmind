using Core.NodeAgent;
using CtraderCliNode;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace UnitTests;

public class DockerServiceTests
{
    [Fact]
    public async Task StartAsync_rejects_image_outside_allowed_prefix()
    {
        var opts = new NodeAgentOptions
        {
            AllowedImagePrefix = "ghcr.io/spotware/",
            DataRoot = Path.Combine(Path.GetTempPath(), "app-agent-test")
        };
        var service = new DockerService(new StaticMonitor(opts), NullLogger<DockerService>.Instance);
        var request = new StartContainerRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Backtest", "evil.example.com/malware:latest",
            ["run"], new Dictionary<string, string>());

        var act = () => service.StartAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<DockerService.ImageNotAllowedException>();
    }

    [Fact]
    public async Task StartAsync_enforces_path_boundary_on_prefix_without_trailing_slash()
    {
        var opts = new NodeAgentOptions
        {
            AllowedImagePrefix = "ghcr.io/spotware",
            DataRoot = Path.Combine(Path.GetTempPath(), "app-agent-test")
        };
        var service = new DockerService(new StaticMonitor(opts), NullLogger<DockerService>.Instance);
        var request = new StartContainerRequest(
            Guid.NewGuid(), Guid.NewGuid(), "Backtest", "ghcr.io/spotware-evil/malware:latest",
            ["run"], new Dictionary<string, string>());

        var act = () => service.StartAsync(request, CancellationToken.None);

        await act.Should().ThrowAsync<DockerService.ImageNotAllowedException>();
    }

    private sealed class StaticMonitor(NodeAgentOptions value) : IOptionsMonitor<NodeAgentOptions>
    {
        public NodeAgentOptions CurrentValue { get; } = value;
        public NodeAgentOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<NodeAgentOptions, string?> listener) => null;
    }
}
