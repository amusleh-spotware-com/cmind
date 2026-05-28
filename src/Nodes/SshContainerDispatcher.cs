using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
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
    private const string DataDir = FilePaths.ContainerDataDir;
    private const string AlgoFile = FilePaths.CbotAlgoFile;
    private const string ParamsFile = FilePaths.ParamsCbotsetFile;
    private const string PwdFile = FilePaths.CtidPwdFile;
    private const string ReportJson = FilePaths.ReportJsonFile;
    private const string ReportHtml = FilePaths.ReportHtmlFile;

    public async Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct)
    {
        if (instance.Node is null) throw new InvalidOperationException("Instance has no node.");
        using var client = Connect(instance.Node);
        using var sftp = ConnectSftp(instance.Node);

        var workDir = $"{instance.Node.DataDirPath.TrimEnd('/')}/{instance.UserId}/{instance.CBotId}/{instance.Id}";
        Run(client, $"mkdir -p {Shell(workDir)}/data");

        var algoPath = $"{workDir}/{AlgoFile}";
        var cbotsetPath = $"{workDir}/{ParamsFile}";
        var pwdPath = $"{workDir}/{PwdFile}";
        using (var ms = new MemoryStream(algoBytes)) sftp.UploadFile(ms, algoPath, true);
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(JsonToCbotset(paramJson))))
            sftp.UploadFile(ms, cbotsetPath, true);

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
        var name = $"{DockerCommands.ContainerNamePrefix}{instance.Type.Name.ToLowerInvariant()}-{instance.Id:N}";
        var labels =
            $"{DockerCommands.LabelFlag} {DockerLabels.User}={instance.UserId} " +
            $"{DockerCommands.LabelFlag} {DockerLabels.Instance}={instance.Id} " +
            $"{DockerCommands.LabelFlag} {DockerLabels.Type}={instance.Type.Name}";
        var mountWork = $"{DockerCommands.VolumeFlag} {Shell(workDir)}:{WorkMount}";

        var cmdArgs = BuildConsoleArgs(instance, ctid);
        var docker = $"docker {DockerCommands.RunDetached} {DockerCommands.NameFlag} {name} {labels} {mountWork} {image} {cmdArgs}";

        log.StartingContainer(instance.Node.Host, docker);
        var result = RunWithOutput(client, docker);
        instance.DataDirSubPath = workDir;
        await Task.CompletedTask;
        return result.Trim();
    }

    public async Task StopAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is null || instance.ContainerId is null) return;
        using var client = Connect(instance.Node);
        Run(client, $"{DockerCommands.Stop} {Shell(instance.ContainerId)} || true");
        Run(client, $"{DockerCommands.RemoveForce} {Shell(instance.ContainerId)} || true");
        await Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> TailLogsAsync(Instance instance, [EnumeratorCancellation] CancellationToken ct)
    {
        if (instance.Node is null || instance.ContainerId is null) yield break;
        using var client = Connect(instance.Node);
        var cmd = client.CreateCommand($"{DockerCommands.LogsFollow} {Shell(instance.ContainerId)}");
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
        using var client = Connect(node);
        var statsLine = RunWithOutput(client, DockerCommands.StatsNoStream);
        var df = RunWithOutput(client, $"df -B1 --output=used,size {Shell(node.DataDirPath)} | tail -n1");
        var du = RunWithOutput(client, $"du -sb {Shell(node.DataDirPath)} 2>/dev/null | awk '{{print $1}}'");
        var (cpu, memUsed, memTotal) = ParseDockerStats(statsLine);
        var (diskUsed, diskTotal) = ParseDf(df);
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

    public async Task<long> GetBacktestDataSizeAsync(Node node, CancellationToken ct)
    {
        using var client = Connect(node);
        var du = RunWithOutput(client, $"du -sb {Shell(node.DataDirPath)} 2>/dev/null | awk '{{print $1}}'");
        await Task.CompletedTask;
        return long.TryParse(du.Trim(), out var v) ? v : 0;
    }

    public async Task CleanBacktestDataAsync(Node node, Guid? userId, CancellationToken ct)
    {
        using var client = Connect(node);
        var root = node.DataDirPath.TrimEnd('/');
        if (!root.StartsWith(FilePaths.CtwDataRootPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to clean outside {FilePaths.CtwDataRootPrefix}");
        var target = userId is null ? $"{root}/*" : $"{root}/{userId}";
        Run(client, $"rm -rf {Shell(target)}");
        await Task.CompletedTask;
    }

    private SshClient Connect(Node node)
    {
        var keyFile = LoadKey(node);
        var client = new SshClient(node.Host, node.SshPort, node.SshUser, keyFile);
        client.Connect();
        return client;
    }

    private SftpClient ConnectSftp(Node node)
    {
        var keyFile = LoadKey(node);
        var client = new SftpClient(node.Host, node.SshPort, node.SshUser, keyFile);
        client.Connect();
        return client;
    }

    private PrivateKeyFile LoadKey(Node node)
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

    private static string Shell(string s) => "'" + s.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string BuildConsoleArgs(Instance i, string ctid)
    {
        var sb = new StringBuilder();
        sb.Append(i.Type.Name == InstanceType.Backtest.Name ? CliCommands.Backtest : CliCommands.Run).Append(' ');
        sb.Append($"{WorkMount}/{AlgoFile} ");
        if (!string.IsNullOrEmpty(ctid))
            sb.Append($"{CliFlags.Ctid} {ctid} {CliFlags.PwdFile} {WorkMount}/{PwdFile} ");
        if (i.TradingAccount is { } ta) sb.Append($"{CliFlags.Account} {ta.AccountNumber} ");
        if (!string.IsNullOrEmpty(i.Symbol)) sb.Append($"{CliFlags.Symbol} {i.Symbol} ");
        if (!string.IsNullOrEmpty(i.Timeframe)) sb.Append($"{CliFlags.Period} {i.Timeframe} ");
        sb.Append($"{CliFlags.DataDir} {DataDir} ");

        if (i.Type.Name == InstanceType.Backtest.Name && !string.IsNullOrEmpty(i.BacktestSettingsJson))
        {
            var doc = JsonDocument.Parse(i.BacktestSettingsJson);
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                var name = p.Name.ToLowerInvariant() switch
                {
                    "from" => "start",
                    "to" => "end",
                    _ => p.Name
                };
                var val = p.Value.ValueKind == JsonValueKind.String
                    ? $"\"{p.Value.GetString()}\""
                    : p.Value.ToString();
                sb.Append($"--{name} {val} ");
            }
            sb.Append($"{CliFlags.ReportJson} {WorkMount}/{ReportJson} {CliFlags.Report} {WorkMount}/{ReportHtml} {CliFlags.ExitOnStop} ");
        }
        return sb.ToString().Trim();
    }

    private static string JsonToCbotset(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var sb = new StringBuilder();
            foreach (var p in doc.RootElement.EnumerateObject())
                sb.AppendLine($"{p.Name}={p.Value}");
            return sb.ToString();
        }
        catch { return json; }
    }

    private static (double, long, long) ParseDockerStats(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return (0, 0, 0);
        var first = line.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var parts = first.Split('|');
        if (parts.Length < 2) return (0, 0, 0);
        double.TryParse(parts[0].Trim().TrimEnd('%'), CultureInfo.InvariantCulture, out var cpu);
        var mem = parts[1].Split('/');
        return (cpu, ParseSize(mem[0]), mem.Length > 1 ? ParseSize(mem[1]) : 0);
    }

    private static (long used, long total) ParseDf(string line)
    {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return (0, 0);
        long.TryParse(parts[0], out var used);
        long.TryParse(parts[1], out var total);
        return (used, total);
    }

    private static long ParseSize(string s)
    {
        s = s.Trim();
        var mult = 1L;
        if (s.EndsWith("GiB", StringComparison.Ordinal)) { mult = 1024L * 1024 * 1024; s = s[..^3]; }
        else if (s.EndsWith("MiB", StringComparison.Ordinal)) { mult = 1024L * 1024; s = s[..^3]; }
        else if (s.EndsWith("KiB", StringComparison.Ordinal)) { mult = 1024L; s = s[..^3]; }
        else if (s.EndsWith("B", StringComparison.Ordinal)) { s = s[..^1]; }
        double.TryParse(s, CultureInfo.InvariantCulture, out var v);
        return (long)(v * mult);
    }
}
