using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Core.Constants;
using Core.NodeAgent;
using Microsoft.Extensions.Options;

namespace ExternalNode;

public sealed class DockerService(IOptionsMonitor<NodeAgentOptions> options, ILogger<DockerService> log)
{
    private const string WorkMount = FilePaths.ContainerWorkMount;
    private const string InstanceLabel = DockerLabels.Instance;

    public sealed class ImageNotAllowedException(string image) : Exception($"Image not allowed: {image}");

    public async Task<StartContainerResponse> StartAsync(StartContainerRequest req, CancellationToken ct)
    {
        var opts = options.CurrentValue;
        // Require a path boundary so "ghcr.io/spotware" cannot match "ghcr.io/spotware-evil/...".
        var allowedPrefix = opts.AllowedImagePrefix.EndsWith('/') ? opts.AllowedImagePrefix : opts.AllowedImagePrefix + "/";
        if (!req.Image.StartsWith(allowedPrefix, StringComparison.Ordinal))
            throw new ImageNotAllowedException(req.Image);

        var workDir = WorkDirFor(req.InstanceId);
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(Path.Combine(workDir, "data"));

        foreach (var (name, base64) in req.Files)
        {
            var safeName = Path.GetFileName(name);
            if (string.IsNullOrEmpty(safeName) || safeName != name)
                throw new InvalidOperationException($"Invalid file name: {name}");
            var bytes = Convert.FromBase64String(base64);
            if (bytes.LongLength > opts.MaxFileBytes)
                throw new InvalidOperationException($"File too large: {name}");
            var path = Path.Combine(workDir, safeName);
            await File.WriteAllBytesAsync(path, bytes, ct);
            if (string.Equals(safeName, FilePaths.CtidPwdFile, StringComparison.Ordinal) && !OperatingSystem.IsWindows())
                await RunCaptureAsync("chmod", ["600", path], opts.ProcessTimeoutSeconds, ct);
        }

        var (pullCode, pullOut) = await RunCaptureAsync(opts.DockerPath, ["pull", req.Image], opts.PullTimeoutSeconds, ct);
        if (pullCode != 0)
        {
            log.LogWarning("docker pull failed for {Image}: {Output}", req.Image, pullOut);
            throw new InvalidOperationException($"docker pull failed: {pullOut}");
        }

        var containerName = $"{DockerCommands.ContainerNamePrefix}{req.Kind.ToLowerInvariant()}-{req.InstanceId:N}";
        var runArgs = new List<string>
        {
            "run", "-d",
            "--name", containerName,
            "--label", $"{DockerLabels.User}={req.UserId}",
            "--label", $"{InstanceLabel}={req.InstanceId}",
            "--label", $"{DockerLabels.Type}={req.Kind}",
            "-v", $"{workDir}:{WorkMount}",
            req.Image
        };
        runArgs.AddRange(req.Args);

        var (runCode, runOut) = await RunCaptureAsync(opts.DockerPath, runArgs, opts.ProcessTimeoutSeconds, ct);
        if (runCode != 0)
        {
            log.LogWarning("docker run failed for {Instance}: {Output}", req.InstanceId, runOut);
            throw new InvalidOperationException($"docker run failed: {runOut}");
        }

        var containerId = runOut.Trim();
        log.LogInformation("Started container {Container} for instance {Instance}", containerId, req.InstanceId);
        return new StartContainerResponse(containerId, workDir);
    }

    public async Task<ContainerStatusResponse> GetStatusAsync(string containerId, CancellationToken ct)
    {
        var opts = options.CurrentValue;
        var (code, output) = await RunCaptureAsync(opts.DockerPath,
            ["inspect", "-f", "{{.State.Running}}|{{.State.ExitCode}}", containerId], opts.ProcessTimeoutSeconds, ct);
        if (code != 0) return new ContainerStatusResponse(false, false, null);
        var parts = output.Trim().Split('|');
        var running = parts.Length > 0 && bool.TryParse(parts[0], out var r) && r;
        int? exit = parts.Length > 1 && int.TryParse(parts[1], out var e) ? e : null;
        return new ContainerStatusResponse(true, running, exit);
    }

    public async Task<string?> ReadReportAsync(string containerId, CancellationToken ct)
    {
        var workDir = await WorkDirForContainerAsync(containerId, ct);
        if (workDir is null) return null;
        var path = Path.Combine(workDir, FilePaths.ReportJsonFile);
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path, ct);
    }

    public async Task StopAsync(string containerId, CancellationToken ct)
    {
        var opts = options.CurrentValue;
        await RunCaptureAsync(opts.DockerPath, ["stop", containerId], opts.ProcessTimeoutSeconds, ct);
        await RunCaptureAsync(opts.DockerPath, ["rm", "-f", containerId], opts.ProcessTimeoutSeconds, ct);
    }

    public async IAsyncEnumerable<string> TailLogsAsync(string containerId, [EnumeratorCancellation] CancellationToken ct)
    {
        var opts = options.CurrentValue;
        var psi = new ProcessStartInfo(opts.DockerPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("logs");
        psi.ArgumentList.Add("-f");
        psi.ArgumentList.Add(containerId);
        using var p = Process.Start(psi)!;
        try
        {
            while (!ct.IsCancellationRequested && !p.HasExited)
            {
                var line = await p.StandardOutput.ReadLineAsync(ct);
                if (line is null) break;
                yield return line;
            }
        }
        finally
        {
            try { if (!p.HasExited) p.Kill(true); } catch { /* swallow */ }
        }
    }

    public async Task<NodeStatsResponse> CollectStatsAsync(CancellationToken ct)
    {
        var opts = options.CurrentValue;
        var (_, statsOut) = await RunCaptureAsync(opts.DockerPath,
            ["stats", "--no-stream", "--format", "{{.CPUPerc}}|{{.MemUsage}}"], opts.ProcessTimeoutSeconds, ct);
        var (cpu, memUsed, memTotal) = ParseDockerStats(statsOut);

        long diskTotal = 0, diskUsed = 0;
        try
        {
            if (Directory.Exists(opts.DataRoot))
            {
                var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(opts.DataRoot)) ?? "/");
                diskTotal = drive.TotalSize;
                diskUsed = drive.TotalSize - drive.AvailableFreeSpace;
            }
        }
        catch { /* swallow */ }

        return new NodeStatsResponse(cpu, memUsed, memTotal, diskUsed, diskTotal, DirSize(opts.DataRoot));
    }

    public Task CleanAsync(Guid? userId, CancellationToken ct)
    {
        var root = options.CurrentValue.DataRoot;
        if (!Directory.Exists(root)) return Task.CompletedTask;
        // Work dirs are keyed by instance id; a user filter is best-effort and clears everything for now.
        foreach (var dir in Directory.EnumerateDirectories(root))
            TryDelete(dir);
        _ = userId;
        return Task.CompletedTask;
    }

    private async Task<string?> WorkDirForContainerAsync(string containerId, CancellationToken ct)
    {
        var opts = options.CurrentValue;
        var (code, output) = await RunCaptureAsync(opts.DockerPath,
            ["inspect", "-f", $"{{{{index .Config.Labels \"{InstanceLabel}\"}}}}", containerId], opts.ProcessTimeoutSeconds, ct);
        if (code != 0) return null;
        return Guid.TryParse(output.Trim(), out var instanceId) ? WorkDirFor(instanceId) : null;
    }

    private string WorkDirFor(Guid instanceId) =>
        Path.Combine(options.CurrentValue.DataRoot, instanceId.ToString("N"));

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { /* swallow */ }
    }

    private static long DirSize(string root)
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

    private static (double cpu, long memUsed, long memTotal) ParseDockerStats(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return (0, 0, 0);
        var first = line.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var parts = first.Split('|');
        if (parts.Length < 2) return (0, 0, 0);
        double.TryParse(parts[0].Trim().TrimEnd('%'), CultureInfo.InvariantCulture, out var cpu);
        var mem = parts[1].Split('/');
        return (cpu, ParseSize(mem[0]), mem.Length > 1 ? ParseSize(mem[1]) : 0);
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

    private static async Task<(int code, string output)> RunCaptureAsync(
        string fileName, IReadOnlyList<string> args, int timeoutSeconds, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { /* swallow */ }
            throw;
        }
        return (p.ExitCode, sb.ToString());
    }
}
