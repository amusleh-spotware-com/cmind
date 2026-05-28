using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core;
using Core.Constants;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Nodes.Builder;

public sealed record BuildResult(bool Success, string Log, byte[]? AlgoBytes);

public sealed class CBotBuilder(
    CtwDbContext db,
    ISecretProtector protector,
    IOptionsMonitor<CtwOptions> options)
{
    private const string DockerExe = "docker";
    private const string NoAlgoMessage = "\nNo .algo file produced.";

    public async Task<BuildResult> BuildAsync(CBotSourceProject project, UserId userId, CancellationToken ct)
    {
        var opts = options.CurrentValue;
        var files = JsonSerializer.Deserialize<Dictionary<string, string>>(project.ProjectFilesJson)
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

        var outDir = Path.Combine(workDir, DockerCommands.BuildOutDir);
        Directory.CreateDirectory(outDir);

        var dockerArgs =
            $"{DockerCommands.RunBuild} {DockerCommands.VolumeFlag} \"{workDir}\":{DockerCommands.BuildMount} " +
            $"{opts.BuildImage} {DockerCommands.BuildCommand}";

        var (code, log) = await RunProcessAsync(DockerExe, dockerArgs, ct);
        var success = code == 0;

        byte[]? algoBytes = null;
        if (success)
        {
            var algoFile = Directory.EnumerateFiles(outDir, DockerCommands.AlgoExtensionPattern, SearchOption.AllDirectories).FirstOrDefault();
            if (algoFile is not null) algoBytes = await File.ReadAllBytesAsync(algoFile, ct);
            else { success = false; log += NoAlgoMessage; }
        }

        project.LastBuildLog = log;
        project.LastBuildAt = DateTimeOffset.UtcNow;
        project.LastBuildSucceeded = success;

        if (success && algoBytes is not null)
        {
            var existing = await db.CBots.FirstOrDefaultAsync(c => c.SourceProjectId == project.Id, ct);
            if (existing is null)
            {
                db.CBots.Add(new CBot
                {
                    UserId = userId,
                    Name = project.Name,
                    EncryptedAlgo = protector.Protect(algoBytes, EncryptionPurposes.CbotAlgo),
                    SourceProjectId = project.Id,
                    Version = 1
                });
            }
            else
            {
                existing.EncryptedAlgo = protector.Protect(algoBytes, EncryptionPurposes.CbotAlgo);
                existing.Version++;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return new BuildResult(success, log, algoBytes);
    }

    private static async Task<(int code, string log)> RunProcessAsync(string fileName, string args, CancellationToken ct)
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
