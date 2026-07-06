using System.Runtime.CompilerServices;
using System.Text;
using Core;
using Core.Constants;
using Core.Logging;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace Nodes;

public sealed class SshContainerDispatcher(
    ISecretProtector protector,
    ILogger<SshContainerDispatcher> log) : IContainerDispatcher
{
    private const string WorkMount = FilePaths.ContainerWorkMount;
    private const string AlgoFile = FilePaths.CbotAlgoFile;
    private const string ParamsFile = FilePaths.ParamsCbotsetFile;
    private const string PwdFile = FilePaths.CtidPwdFile;

    public async Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct)
    {
        if (instance.Node is not RemoteNode remote)
            throw new InvalidOperationException("Instance has no remote node.");
        using var client = Connect(remote);
        using var sftp = ConnectSftp(remote);

        var workDir = $"{remote.DataDirPath.TrimEnd('/')}/{instance.UserId.Value}/{instance.CBotId.Value}/{instance.Id.Value}";
        Run(client, $"mkdir -p {Shell(workDir)}/data");

        var algoPath = $"{workDir}/{AlgoFile}";
        var cbotsetPath = $"{workDir}/{ParamsFile}";
        var pwdPath = $"{workDir}/{PwdFile}";
        using (var ms = new MemoryStream(algoBytes)) sftp.UploadFile(ms, algoPath, true);
        var cbotset = ContainerCommandHelpers.JsonToCbotset(paramJson);
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(cbotset)))
            sftp.UploadFile(ms, cbotsetPath, true);
        var hasParams = !string.IsNullOrWhiteSpace(cbotset);

        var ctid = string.Empty;
        if (instance.TradingAccount is { } ta && ta.CTid is not null)
        {
            ctid = ta.CTid.Username;
            var pwd = protector.Unprotect(ta.CTid.EncryptedPassword, EncryptionPurposes.CtidPassword);
            using var ms = new MemoryStream(pwd);
            sftp.UploadFile(ms, pwdPath, true);
            Run(client, $"chmod 600 {Shell(pwdPath)}");
        }

        var image = $"{DockerImages.CtraderConsole}:{instance.DockerImageTag}";
        var name = $"{DockerCommands.ContainerNamePrefix}{instance.KindName.ToLowerInvariant()}-{instance.Id.Value:N}";
        var labels =
            $"{DockerCommands.LabelFlag} {DockerLabels.User}={instance.UserId.Value} " +
            $"{DockerCommands.LabelFlag} {DockerLabels.Instance}={instance.Id.Value} " +
            $"{DockerCommands.LabelFlag} {DockerLabels.Type}={instance.KindName}";
        var mountWork = $"{DockerCommands.VolumeFlag} {Shell(workDir)}:{WorkMount}";

        var cmdArgs = ContainerCommandHelpers.BuildConsoleArgs(instance, ctid, hasParams);
        var docker = $"docker {DockerCommands.RunDetached} {DockerCommands.NameFlag} {name} {labels} {mountWork} {image} {cmdArgs}";

        log.StartingContainer(remote.Host, docker);
        var result = RunWithOutput(client, docker);
        instance.DataDirSubPath = workDir;
        await Task.CompletedTask;
        return result.Trim();
    }

    public async Task StopAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not RemoteNode remote) return;
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (containerId is null) return;
        using var client = Connect(remote);
        Run(client, $"{DockerCommands.Stop} {Shell(containerId)} || true");
        Run(client, $"{DockerCommands.RemoveForce} {Shell(containerId)} || true");
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> TailLogsAsync(Instance instance, [EnumeratorCancellation] CancellationToken ct)
    {
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (instance.Node is not RemoteNode remote || containerId is null) yield break;
        using var client = Connect(remote);
        var cmd = client.CreateCommand($"{DockerCommands.LogsFollow} {Shell(containerId)}");
        var async = cmd.BeginExecute();
        using var reader = new StreamReader(cmd.OutputStream);
        while (!ct.IsCancellationRequested && !async.IsCompleted)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null) { await Task.Delay(200, ct); continue; }
            yield return line;
        }
        cmd.EndExecute(async);
    }

    public async Task<NodeStats> CollectStatsAsync(Node node, CancellationToken ct)
    {
        if (node is not RemoteNode remote) throw new InvalidOperationException("Not a remote node.");
        using var client = Connect(remote);
        var statsLine = RunWithOutput(client, DockerCommands.StatsNoStream);
        var df = RunWithOutput(client, $"df -B1 --output=used,size {Shell(remote.DataDirPath)} | tail -n1");
        var du = RunWithOutput(client, $"du -sb {Shell(remote.DataDirPath)} 2>/dev/null | awk '{{print $1}}'");
        var (cpu, memUsed, memTotal) = ContainerCommandHelpers.ParseDockerStats(statsLine);
        var (diskUsed, diskTotal) = ContainerCommandHelpers.ParseDf(df);
        long.TryParse(du.Trim(), out var backtestUsed);
        await Task.CompletedTask;
        return new NodeStats
        {
            NodeId = node.Id,
            CpuPercent = cpu,
            MemUsedBytes = memUsed,
            MemTotalBytes = memTotal,
            DiskUsedBytes = diskUsed,
            DiskTotalBytes = diskTotal,
            BacktestDataUsedBytes = backtestUsed,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public async Task<bool?> IsRunningAsync(Instance instance, CancellationToken ct)
    {
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (instance.Node is not RemoteNode remote || containerId is null) return null;
        using var client = Connect(remote);
        var r = client.RunCommand($"docker inspect -f {{{{.State.Running}}}} {Shell(containerId)}");
        await Task.CompletedTask;
        if (r.ExitStatus != 0) return null;
        return bool.TryParse(r.Result.Trim(), out var running) && running;
    }

    public async Task<int?> GetExitCodeAsync(Instance instance, CancellationToken ct)
    {
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (instance.Node is not RemoteNode remote || containerId is null) return null;
        using var client = Connect(remote);
        var r = client.RunCommand($"docker inspect -f {{{{.State.ExitCode}}}} {Shell(containerId)}");
        await Task.CompletedTask;
        if (r.ExitStatus != 0) return null;
        return int.TryParse(r.Result.Trim(), out var exit) ? exit : null;
    }

    public async Task<string?> ReadReportAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not RemoteNode remote || instance.DataDirSubPath is not { } workDir) return null;
        using var sftp = ConnectSftp(remote);
        var path = $"{workDir}/{FilePaths.ReportJsonFile}";
        await Task.CompletedTask;
        if (!sftp.Exists(path)) return null;
        using var ms = new MemoryStream();
        sftp.DownloadFile(path, ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public async Task<long> GetBacktestDataSizeAsync(Node node, CancellationToken ct)
    {
        if (node is not RemoteNode remote) return 0;
        using var client = Connect(remote);
        var du = RunWithOutput(client, $"du -sb {Shell(remote.DataDirPath)} 2>/dev/null | awk '{{print $1}}'");
        await Task.CompletedTask;
        return long.TryParse(du.Trim(), out var v) ? v : 0;
    }

    public async Task CleanBacktestDataAsync(Node node, UserId? userId, CancellationToken ct)
    {
        if (node is not RemoteNode remote) return;
        using var client = Connect(remote);
        var root = remote.DataDirPath.TrimEnd('/');
        if (!root.StartsWith(FilePaths.CtwDataRootPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to clean outside {FilePaths.CtwDataRootPrefix}");
        var target = userId is null ? $"{root}/*" : $"{root}/{userId.Value.Value}";
        Run(client, $"rm -rf {Shell(target)}");
        await Task.CompletedTask;
    }

    private SshClient Connect(RemoteNode node)
    {
        var keyFile = LoadKey(node);
        var client = new SshClient(node.Host, node.SshPort, node.SshUser, keyFile);
        client.Connect();
        return client;
    }

    private SftpClient ConnectSftp(RemoteNode node)
    {
        var keyFile = LoadKey(node);
        var client = new SftpClient(node.Host, node.SshPort, node.SshUser, keyFile);
        client.Connect();
        return client;
    }

    private PrivateKeyFile LoadKey(RemoteNode node)
    {
        var key = protector.Unprotect(node.EncryptedSshKey, EncryptionPurposes.NodeSshKey);
        var ms = new MemoryStream(key);
        var passphrase = node.EncryptedSshKeyPassphrase is null
            ? null
            : Encoding.UTF8.GetString(protector.Unprotect(node.EncryptedSshKeyPassphrase, EncryptionPurposes.NodeSshPassphrase));
        return passphrase is null ? new PrivateKeyFile(ms) : new PrivateKeyFile(ms, passphrase);
    }

    private static void Run(SshClient c, string cmd)
    {
        var r = c.RunCommand(cmd);
        if (r.ExitStatus != 0) throw new InvalidOperationException($"SSH '{cmd}' failed: {r.Error}");
    }

    private static string RunWithOutput(SshClient c, string cmd)
    {
        var r = c.RunCommand(cmd);
        if (r.ExitStatus != 0) throw new InvalidOperationException($"SSH '{cmd}' failed: {r.Error}");
        return r.Result;
    }

    private static string Shell(string s) => ContainerCommandHelpers.ShellSingleQuote(s);
}
