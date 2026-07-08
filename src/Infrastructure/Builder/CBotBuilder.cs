using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Builder;

public sealed record BuildResult(bool Success, string Log, byte[]? AlgoBytes);

public sealed class CBotBuilder(
    DataContext db,
    ISecretProtector protector,
    IOptionsMonitor<AppOptions> options,
    ILogger<CBotBuilder> log)
{
    private const string DockerExe = "docker";
    private const int MaxLogChars = 4000;
    private const string OutDirName = DockerCommands.BuildOutDir;
    private const string NoAlgoMessage = "\nNo .algo file produced.";
    private const string NoProjectFileMessage = "No .csproj file found in project.";

    public async Task<BuildResult> BuildAsync(CBotSourceProject project, UserId userId, CancellationToken ct)
    {
        var opts = options.CurrentValue;
        var projectFilesJson = project.EncryptedProjectFiles.Length == 0
            ? "{}"
            : Encoding.UTF8.GetString(protector.Unprotect(project.EncryptedProjectFiles, EncryptionPurposes.CbotSource));
        var files = JsonSerializer.Deserialize<Dictionary<string, string>>(projectFilesJson)
                    ?? new Dictionary<string, string>();

        var workDir = Path.Combine(opts.BuildWorkRoot, userId.Value.ToString("N"), project.Id.Value.ToString("N"));
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(workDir, rel.Replace('\\', '/'));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, content, ct);
        }

        var outDir = Path.Combine(workDir, OutDirName);
        Directory.CreateDirectory(outDir);

        var projectFile = Directory.EnumerateFiles(workDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (projectFile is null)
        {
            project.RecordBuild(NoProjectFileMessage, false);
            await db.SaveChangesAsync(ct);
            return new BuildResult(false, NoProjectFileMessage, null);
        }

        // Build inside a throwaway container so untrusted user MSBuild targets cannot
        // reach the host filesystem/network. The work dir is bind-mounted at /work and
        // the build writes its output to /work/out.
        var dockerArgs =
            $"{DockerCommands.RunBuild} " +
            $"{DockerCommands.VolumeFlag} {DockerCommands.BuildNugetVolume}:{DockerCommands.BuildNugetMount} " +
            $"{DockerCommands.VolumeFlag} \"{workDir}\":{DockerCommands.BuildMount} " +
            $"{opts.BuildImage} {DockerCommands.BuildCommand}";
        var (code, buildLog) = await RunProcessAsync(DockerExe, dockerArgs, workDir, ct);
        var success = code == 0;
        if (!success) log.LocalBuildFailed(buildLog.Length > MaxLogChars ? buildLog[^MaxLogChars..] : buildLog);

        byte[]? algoBytes = null;
        if (success)
        {
            var algoFile = Directory.EnumerateFiles(outDir, "*.algo", SearchOption.AllDirectories).FirstOrDefault();
            if (algoFile is not null) algoBytes = await File.ReadAllBytesAsync(algoFile, ct);
            else { success = false; buildLog += NoAlgoMessage; }
        }

        project.RecordBuild(buildLog, success);

        if (success && algoBytes is not null)
        {
            var existing = await db.CBots.FirstOrDefaultAsync(
                c => c.SourceProjectId == project.Id
                     || (c.UserId == userId && c.Name == project.Name),
                ct);
            if (existing is null)
            {
                db.CBots.Add(CBot.Create(userId, project.Name,
                    protector.Protect(algoBytes, EncryptionPurposes.CbotAlgo), project.Id));
            }
            else
            {
                existing.UpdateAlgo(protector.Protect(algoBytes, EncryptionPurposes.CbotAlgo), project.Id);
            }
        }
        await db.SaveChangesAsync(ct);
        return new BuildResult(success, buildLog, algoBytes);
    }

    private static async Task<(int code, string log)> RunProcessAsync(string fileName, string args, string workingDir, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(fileName, args)
        {
            WorkingDirectory = workingDir,
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
