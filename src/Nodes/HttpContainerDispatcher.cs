using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using Core;
using Core.Constants;
using Core.NodeAgent;
using Microsoft.IdentityModel.Tokens;

namespace Nodes;

/// <summary>
/// Dispatches container operations to an external node agent over HTTP, authenticating
/// with a short-lived per-node JWT signed with that node's shared secret.
/// </summary>
public sealed class HttpContainerDispatcher(
    IHttpClientFactory httpClientFactory,
    ISecretProtector protector,
    TimeProvider timeProvider) : IContainerDispatcher
{
    public const string ReadClientName = "node-agent-read";
    public const string WriteClientName = "node-agent-write";
    public const string StreamClientName = "node-agent-stream";

    private const string AlgoFile = FilePaths.CbotAlgoFile;
    private const string ParamsFile = FilePaths.ParamsCbotsetFile;
    private const string PwdFile = FilePaths.CtidPwdFile;

    public async Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct)
    {
        var node = RequireRemote(instance);

        var files = new Dictionary<string, string> { [AlgoFile] = Convert.ToBase64String(algoBytes) };
        var cbotset = ContainerCommandHelpers.JsonToCbotset(paramJson);
        var hasParams = !string.IsNullOrWhiteSpace(cbotset);
        if (hasParams) files[ParamsFile] = Convert.ToBase64String(Encoding.UTF8.GetBytes(cbotset));

        var ctid = string.Empty;
        if (instance.TradingAccount is { CTid: not null } ta)
        {
            ctid = ta.CTid.Username;
            var pwd = protector.Unprotect(ta.CTid.EncryptedPassword, EncryptionPurposes.CtidPassword);
            files[PwdFile] = Convert.ToBase64String(pwd);
        }

        var args = ContainerCommandHelpers.BuildConsoleArgsList(instance, ctid, hasParams);
        var image = $"{DockerImages.CtraderConsole}:{instance.DockerImageTag}";
        var request = new StartContainerRequest(instance.Id.Value, instance.UserId.Value, instance.KindName,
            image, args, files, ContainerCommandHelpers.DataScopeFor(instance));

        using var client = CreateClient(node, WriteClientName);
        using var response = await client.PostAsJsonAsync(NodeAgentRoutes.Start, request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Agent start failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(ct)}");
        var result = await response.Content.ReadFromJsonAsync<StartContainerResponse>(ct)
                     ?? throw new InvalidOperationException("Agent returned empty start response.");
        instance.SetDataDirSubPath(result.WorkDir);
        return result.ContainerId;
    }

    public async Task StopAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not CtraderCliNode node) return;
        if (ContainerCommandHelpers.GetContainerId(instance) is not { } containerId) return;
        using var client = CreateClient(node, WriteClientName);
        using var response = await client.PostAsync(NodeAgentRoutes.Stop(containerId), null, ct);
    }

    public async Task<bool?> IsRunningAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not CtraderCliNode node) return null;
        if (ContainerCommandHelpers.GetContainerId(instance) is not { } containerId) return null;
        var status = await GetStatusAsync(node, containerId, ct);
        if (status is null || !status.Exists) return null;
        return status.Running;
    }

    public async Task<int?> GetExitCodeAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not CtraderCliNode node) return null;
        if (ContainerCommandHelpers.GetContainerId(instance) is not { } containerId) return null;
        var status = await GetStatusAsync(node, containerId, ct);
        return status?.ExitCode;
    }

    public async Task<string?> ReadReportAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not CtraderCliNode node) return null;
        if (ContainerCommandHelpers.GetContainerId(instance) is not { } containerId) return null;
        using var client = CreateClient(node, ReadClientName);
        using var response = await client.GetAsync(NodeAgentRoutes.Report(containerId), ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async IAsyncEnumerable<string> TailLogsAsync(Instance instance, [EnumeratorCancellation] CancellationToken ct)
    {
        if (instance.Node is not CtraderCliNode node) yield break;
        if (ContainerCommandHelpers.GetContainerId(instance) is not { } containerId) yield break;
        using var client = CreateClient(node, StreamClientName);
        using var stream = await client.GetStreamAsync(NodeAgentRoutes.Logs(containerId), ct);
        using var reader = new StreamReader(stream);
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) break;
            yield return line;
        }
    }

    public async Task<NodeStats> CollectStatsAsync(Node node, CancellationToken ct)
    {
        var remote = node as CtraderCliNode ?? throw new InvalidOperationException("Not a remote node.");
        using var client = CreateClient(remote, ReadClientName);
        var stats = await client.GetFromJsonAsync<NodeStatsResponse>(NodeAgentRoutes.NodeStats, ct)
                    ?? throw new InvalidOperationException("Agent returned empty stats.");
        return NodeStats.Create(node.Id, stats.CpuPercent, stats.MemUsedBytes, stats.MemTotalBytes,
            stats.DiskUsedBytes, stats.DiskTotalBytes, stats.BacktestDataUsedBytes, timeProvider.GetUtcNow());
    }

    public async Task<long> GetBacktestDataSizeAsync(Node node, CancellationToken ct)
    {
        if (node is not CtraderCliNode remote) return 0;
        using var client = CreateClient(remote, ReadClientName);
        var stats = await client.GetFromJsonAsync<NodeStatsResponse>(NodeAgentRoutes.NodeStats, ct);
        return stats?.BacktestDataUsedBytes ?? 0;
    }

    public async Task CleanBacktestDataAsync(Node node, UserId? userId, CancellationToken ct)
    {
        if (node is not CtraderCliNode remote) return;
        using var client = CreateClient(remote, WriteClientName);
        var url = userId is { } uid ? $"{NodeAgentRoutes.NodeClean}?userId={uid.Value}" : NodeAgentRoutes.NodeClean;
        using var response = await client.PostAsync(url, null, ct);
    }

    private async Task<ContainerStatusResponse?> GetStatusAsync(CtraderCliNode node, string containerId, CancellationToken ct)
    {
        using var client = CreateClient(node, ReadClientName);
        return await client.GetFromJsonAsync<ContainerStatusResponse>(NodeAgentRoutes.Status(containerId), ct);
    }

    private static CtraderCliNode RequireRemote(Instance instance) =>
        instance.Node as CtraderCliNode ?? throw new InvalidOperationException("Instance has no remote node.");

    private HttpClient CreateClient(CtraderCliNode node, string clientName)
    {
        var client = httpClientFactory.CreateClient(clientName);
        client.BaseAddress = new Uri(node.BaseUrl);
        var secret = Encoding.UTF8.GetString(protector.Unprotect(node.EncryptedApiSecret, EncryptionPurposes.NodeApiSecret));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(secret, timeProvider.GetUtcNow().UtcDateTime));
        client.DefaultRequestHeaders.Add(NodeAgentProtocol.HeaderName, NodeAgentProtocol.Version.ToString());
        return client;
    }

    private static string CreateToken(string secret, DateTime now)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: NodeAgentAuth.Issuer,
            audience: NodeAgentAuth.Audience,
            notBefore: now,
            expires: now.AddSeconds(NodeAgentAuth.TokenLifetimeSeconds),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
