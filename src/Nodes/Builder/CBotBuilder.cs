using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Core;
using Core.Constants;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.Builder;

public sealed record BuildResult(bool Success, string Log, byte[]? AlgoBytes);

public sealed class CBotBuilder
{
    private readonly CtwDbContext _db;
    private readonly ISecretProtector _protector;
    private readonly ILogger<CBotBuilder> _log;
    private readonly IOptionsMonitor<CtwOptions> _options;

    public CBotBuilder(CtwDbContext db, ISecretProtector protector, ILogger<CBotBuilder> log,
        IOptionsMonitor<CtwOptions> options)
    {
        _db = db;
        _protector = protector;
        _log = log;
        _options = options;
    }

    public async Task<BuildResult> BuildAsync(CBotSourceProject project, Guid userId, CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        var files = JsonSerializer.Deserialize<Dictionary<string, string>>(project.ProjectFilesJson)
                    ?? new Dictionary<string, string>();

        var workDir = Path.Combine(opts.BuildWorkRoot, userId.ToString("N"), project.Id.ToString("N"));
        if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
        Directory.CreateDirectory(workDir);

        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(workDir, rel.Replace('\\', '/'));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, content, ct);
        }

        var outDir = Path.Combine(workDir, "out");
        Directory.CreateDirectory(outDir);

        var image = opts.BuildImage;
        var args = "sh -c \"cd /work && dotnet build -c Release -o /work/out 2>&1\"";

        var dockerArgs = $"run --rm --network=none --memory=1g --cpus=1 " +
                         $"-v \"{workDir}\":/work {image} {args}";

        var (code, log) = await RunProcessAsync("docker", dockerArgs, ct);
        var success = code == 0;

        byte[]? algoBytes = null;
        if (success)
        {
            var algoFile = Directory.EnumerateFiles(outDir, "*.algo", SearchOption.AllDirectories).FirstOrDefault();
            if (algoFile is not null) algoBytes = await File.ReadAllBytesAsync(algoFile, ct);
            else { success = false; log += "\nNo .algo file produced."; }
        }

        project.LastBuildLog = log;
        project.LastBuildAt = DateTimeOffset.UtcNow;
        project.LastBuildSucceeded = success;

        if (success && algoBytes is not null)
        {
            var existing = await _db.CBots.FirstOrDefaultAsync(c => c.SourceProjectId == project.Id, ct);
            if (existing is null)
            {
                _db.CBots.Add(new CBot
                {
                    UserId = userId,
                    Name = project.Name,
                    EncryptedAlgo = _protector.Protect(algoBytes, "cbot.algo"),
                    SourceProjectId = project.Id,
                    Version = 1
                });
            }
            else
            {
                existing.EncryptedAlgo = _protector.Protect(algoBytes, "cbot.algo");
                existing.Version++;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
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
