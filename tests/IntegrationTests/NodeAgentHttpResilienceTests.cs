using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nodes;
using Xunit;

namespace IntegrationTests;

public sealed class NodeAgentHttpResilienceTests
{
    private sealed class SequenceHandler(int failTimes) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var n = Interlocked.Increment(ref _calls);
            var status = n <= failTimes ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private static (IHttpClientFactory Factory, SequenceHandler Read, SequenceHandler Write) Build(
        int readFailTimes, int writeFailTimes)
    {
        var read = new SequenceHandler(readFailTimes);
        var write = new SequenceHandler(writeFailTimes);
        var services = new ServiceCollection();
        services.AddNodeAgentHttpClients();
        services.AddHttpClient(HttpContainerDispatcher.ReadClientName).ConfigurePrimaryHttpMessageHandler(() => read);
        services.AddHttpClient(HttpContainerDispatcher.WriteClientName).ConfigurePrimaryHttpMessageHandler(() => write);
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IHttpClientFactory>(), read, write);
    }

    [Fact]
    public async Task Read_client_retries_transient_failures_then_succeeds()
    {
        var (factory, read, _) = Build(readFailTimes: 2, writeFailTimes: 0);
        var client = factory.CreateClient(HttpContainerDispatcher.ReadClientName);

        var response = await client.GetAsync("http://node/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        read.Calls.Should().Be(3); // initial attempt + 2 retries
    }

    [Fact]
    public async Task Write_client_never_retries_a_non_idempotent_failure()
    {
        var (factory, _, write) = Build(readFailTimes: 0, writeFailTimes: 1);
        var client = factory.CreateClient(HttpContainerDispatcher.WriteClientName);

        var response = await client.PostAsync("http://node/stop", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        write.Calls.Should().Be(1); // no retry — a retried start could double-launch a container
    }
}
