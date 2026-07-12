using System.Net.Http.Headers;
using System.Net.Http.Json;
using Core.Constants;
using Core.Logging;
using Core.NodeAgent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CtraderCliNode;

/// <summary>
/// Self-registers this agent with the main node and heartbeats on an interval. When MainUrl or
/// AdvertiseUrl is unset the agent stays in manual-registration mode and this loop is a no-op.
/// </summary>
public sealed class NodeRegistrationClient(
    IHttpClientFactory httpClientFactory,
    IOptions<NodeAgentOptions> options,
    ILogger<NodeRegistrationClient> log) : BackgroundService
{
    public const string HttpClientName = "main-registrar";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.MainUrl) || string.IsNullOrWhiteSpace(o.AdvertiseUrl))
            return;

        var interval = TimeSpan.FromSeconds(o.HeartbeatIntervalSeconds <= 0 ? 30 : o.HeartbeatIntervalSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RegisterAsync(o, stoppingToken); }
            catch (Exception ex) { log.AgentRegistrationFailed(ex.Message); }
            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task RegisterAsync(NodeAgentOptions o, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        client.BaseAddress = new Uri(o.MainUrl);

        var name = string.IsNullOrWhiteSpace(o.NodeName) ? Environment.MachineName : o.NodeName;
        var request = new NodeRegistrationRequest(
            name, o.AdvertiseUrl, o.Mode, o.MaxInstances, o.DataRoot, NodeAgentProtocol.Version);

        using var message = new HttpRequestMessage(HttpMethod.Post, NodeDiscoveryRoutes.Register)
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(AuthSchemes.Bearer, o.JwtSecret);

        using var response = await client.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            log.AgentRegistrationFailed($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            return;
        }

        var body = await response.Content.ReadFromJsonAsync<NodeRegistrationResponse>(ct);
        if (body is not null) log.AgentRegistered(o.MainUrl, body.NodeId);
    }
}
