using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Core;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes;

public sealed class LocalContainerDispatcher(
    ISecretProtector protector,
    IOptionsMonitor<AppOptions> options,
    ILogger<LocalContainerDispatcher> log,
    TimeProvider timeProvider) : IContainerDispatcher
{
    private const string WorkMount = FilePaths.ContainerWorkMount;
    private const string DataDir = FilePaths.ContainerDataDir;
    private const string AlgoFile = FilePaths.CbotAlgoFile;
    private const string ParamsFile = FilePaths.ParamsCbotsetFile;
    private const string PwdFile = FilePaths.CtidPwdFile;

    public async Task<string> StartAsync(Instance instance, byte[] algoBytes, string paramJson, CancellationToken ct)
    {
        if (instance.Node is not LocalNode) throw new InvalidOperationException("Not a local node.");

        var workRoot = options.CurrentValue.LocalNode.WorkRoot;
        var workDir = Path.Combine(workRoot,
            instance.UserId.Value.ToString("N"),
            instance.CBotId.Value.ToString("N"),
            instance.Id.Value.ToString("N"));
        // Downloaded market data belongs to a trading account (its broker data source) and is reusable across
        // every backtest on that account, so cache it in a STABLE per-account dir mounted at /mnt/data (a
        // SEPARATE, non-nested mount) — NOT under the per-instance work dir (whose id changes every run, which
        // is what forced cTrader to re-download the data on each backtest).
        var sharedDataDir = Path.Combine(workRoot, FilePaths.SharedMarketDataDirName,
            ContainerCommandHelpers.DataScopeFor(instance));
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(sharedDataDir);

        await File.WriteAllBytesAsync(Path.Combine(workDir, AlgoFile), algoBytes, ct);
        var cbotset = ContainerCommandHelpers.JsonToCbotset(paramJson);
        await File.WriteAllTextAsync(Path.Combine(workDir, ParamsFile), cbotset, ct);
        var hasParams = !string.IsNullOrWhiteSpace(cbotset);

        var ctid = string.Empty;
        if (instance.TradingAccount is { } ta && ta.CTid is not null)
        {
            ctid = ta.CTid.Username;
            var pwd = protector.Unprotect(ta.CTid.EncryptedPassword, EncryptionPurposes.CtidPassword);
            var pwdPath = Path.Combine(workDir, PwdFile);
            await File.WriteAllBytesAsync(pwdPath, pwd, ct);
            if (!OperatingSystem.IsWindows())
                await RunProcessAsync("chmod", $"600 \"{pwdPath}\"", ct);
        }

        var image = $"{DockerImages.CtraderConsole}:{instance.DockerImageTag}";
        var name = $"{DockerCommands.ContainerNamePrefix}{instance.KindName.ToLowerInvariant()}-{instance.Id.Value:N}";
        var cmdArgs = ContainerCommandHelpers.BuildConsoleArgs(instance, ctid, hasParams);

        var dockerArgs = new StringBuilder();
        dockerArgs.Append(DockerCommands.RunDetached).Append(' ');
        dockerArgs.Append(DockerCommands.NameFlag).Append(' ').Append(name).Append(' ');
        dockerArgs.Append(DockerCommands.LabelFlag).Append(' ').Append(DockerLabels.User).Append('=').Append(instance.UserId.Value).Append(' ');
        dockerArgs.Append(DockerCommands.LabelFlag).Append(' ').Append(DockerLabels.Instance).Append('=').Append(instance.Id.Value).Append(' ');
        dockerArgs.Append(DockerCommands.LabelFlag).Append(' ').Append(DockerLabels.Type).Append('=').Append(instance.KindName).Append(' ');
        dockerArgs.Append(DockerCommands.VolumeFlag).Append(' ').Append('"').Append(workDir).Append('"').Append(':').Append(WorkMount).Append(' ');
        dockerArgs.Append(DockerCommands.VolumeFlag).Append(' ').Append('"').Append(sharedDataDir).Append('"').Append(':').Append(DataDir).Append(' ');
        dockerArgs.Append(image).Append(' ').Append(cmdArgs);

        log.StartingContainer("local", dockerArgs.ToString());
        var (code, output) = await RunProcessAsync("docker", dockerArgs.ToString(), ct);
        if (code != 0)
        {
            log.LocalDockerFailed(dockerArgs.ToString(), output);
            throw new InvalidOperationException($"docker run failed: {output}");
        }
        instance.SetDataDirSubPath(workDir);
        return output.Trim();
    }

    public async Task StopAsync(Instance instance, CancellationToken ct)
    {
        if (instance.Node is not LocalNode) return;
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (containerId is null) return;
        await RunProcessAsync("docker", $"stop {containerId}", ct);
        await RunProcessAsync("docker", $"rm -f {containerId}", ct);
    }

    public async IAsyncEnumerable<string> TailLogsAsync(Instance instance, [EnumeratorCancellation] CancellationToken ct)
    {
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (instance.Node is not LocalNode || containerId is null) yield break;

        var psi = new ProcessStartInfo("docker", $"logs -f {containerId}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        try
        {
            while (!ct.IsCancellationRequested && !p.HasExited)
            {
                var line = await p.StandardOutput.ReadLineAsync(ct);
                if (line is null) { await Task.Delay(200, ct); continue; }
                yield return line;
            }
        }
        finally
        {
            try { if (!p.HasExited) p.Kill(true); } catch { /* swallow */ }
        }
    }

    public async Task<NodeStats> CollectStatsAsync(Node node, CancellationToken ct)
    {
        if (node is not LocalNode) throw new InvalidOperationException("Not a local node.");
        var (_, statsOut) = await RunProcessAsync("docker", "stats --no-stream --format {{.CPUPerc}}|{{.MemUsage}}", ct);
        var (cpu, memUsed, memTotal) = ContainerCommandHelpers.ParseDockerStats(statsOut);
        var workRoot = options.CurrentValue.LocalNode.WorkRoot;
        var diskTotal = 0L;
        var diskUsed = 0L;
        try
        {
            if (Directory.Exists(workRoot))
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(workRoot)) ?? "/");
                diskTotal = drive.TotalSize;
                diskUsed = drive.TotalSize - drive.AvailableFreeSpace;
            }
        }
        catch { /* swallow */ }
        var backtestUsed = SafeDirSize(workRoot);
        return NodeStats.Create(node.Id, cpu, memUsed, memTotal, diskUsed, diskTotal, backtestUsed,
            timeProvider.GetUtcNow());
    }

    public async Task<bool?> IsRunningAsync(Instance instance, CancellationToken ct)
    {
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (containerId is null) return null;
        var (code, output) = await RunProcessAsync("docker", $"inspect -f {{{{.State.Running}}}} {containerId}", ct);
        if (code != 0) return null;
        return bool.TryParse(output.Trim(), out var running) && running;
    }

    public async Task<int?> GetExitCodeAsync(Instance instance, CancellationToken ct)
    {
        var containerId = ContainerCommandHelpers.GetContainerId(instance);
        if (containerId is null) return null;
        var (code, output) = await RunProcessAsync("docker", $"inspect -f {{{{.State.ExitCode}}}} {containerId}", ct);
        if (code != 0) return null;
        return int.TryParse(output.Trim(), out var exit) ? exit : null;
    }

    public async Task<string?> ReadReportAsync(Instance instance, CancellationToken ct)
    {
        if (instance.DataDirSubPath is not { } workDir) return null;
        var path = Path.Combine(workDir, FilePaths.ReportJsonFile);
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    public Task<long> GetBacktestDataSizeAsync(Node node, CancellationToken ct)
    {
        if (node is not LocalNode) return Task.FromResult(0L);
        return Task.FromResult(SafeDirSize(options.CurrentValue.LocalNode.WorkRoot));
    }

    public Task CleanBacktestDataAsync(Node node, UserId? userId, CancellationToken ct)
    {
        if (node is not LocalNode) return Task.CompletedTask;
        var root = options.CurrentValue.LocalNode.WorkRoot;
        if (!Directory.Exists(root)) return Task.CompletedTask;
        if (userId is null)
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
                TryDelete(dir);
        }
        else
        {
            var userDir = Path.Combine(root, userId.Value.Value.ToString("N"));
            TryDelete(userDir);
        }
        return Task.CompletedTask;
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* swallow */ }
    }

    private static long SafeDirSize(string root)
    {
        try
        {
            if (!Directory.Exists(root)) return 0;
            var total = 0L;
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { /* swallow */ }
            }
            return total;
        }
        catch { return 0; }
    }

    private static async Task<(int code, string output)> RunProcessAsync(string fileName, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        await p.WaitForExitAsync(ct);
        return (p.ExitCode, sb.ToString());
    }
}
