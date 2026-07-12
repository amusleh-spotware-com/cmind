using System.Diagnostics;
using System.Globalization;
using System.Text;
using Core;
using Core.Accounts;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Accounts;

/// <summary>
/// Verifies a trading account's broker by running the shipped broker-probe cBot through the cTrader CLI on
/// the web host (the same host that builds cBots — it has the Docker socket). The probe logs in with the
/// cID credentials, prints <c>Account.BrokerName</c> in the <see cref="BrokerProbeOutput"/> marker format,
/// and stops; we read that back from the container logs. Only invoked when the deployment's broker
/// allowlist is restricted. Fails closed (never throws into the request) and cleans up its work dir.
/// </summary>
public sealed class BrokerVerifier(
    ISecretProtector protector,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider,
    ILogger<BrokerVerifier> log) : IBrokerVerifier
{
    private const string DockerExe = "docker";
    private const string WorkMount = FilePaths.ContainerWorkMount;

    public async Task<BrokerVerificationResult> VerifyAsync(BrokerProbeRequest request, CancellationToken ct)
    {
        var accounts = options.CurrentValue.Accounts;
        var algoPath = accounts.BrokerProbeAlgoPath;
        if (string.IsNullOrWhiteSpace(algoPath) || !File.Exists(algoPath))
        {
            log.BrokerProbeUnavailable(request.AccountNumber, $"probe algo not found at '{algoPath}'");
            return BrokerVerificationResult.Failed(BrokerVerificationError.ProbeFailed);
        }

        var workDir = Path.Combine(Path.GetTempPath(), "cmind", "broker-probe", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        try
        {
            File.Copy(algoPath, Path.Combine(workDir, FilePaths.BrokerProbeAlgoFile));
            var pwdPath = Path.Combine(workDir, FilePaths.CtidPwdFile);
            var pwd = protector.Unprotect(request.EncryptedPassword, EncryptionPurposes.CtidPassword);
            await File.WriteAllBytesAsync(pwdPath, pwd, ct);
            if (!OperatingSystem.IsWindows())
                await RunAsync(["chmod", "600", pwdPath], Timeout.InfiniteTimeSpan, ct);

            var image = $"{options.CurrentValue.DefaultDockerImage}:{options.CurrentValue.DefaultDockerTag}";
            var args = new List<string>
            {
                "run", "--rm", DockerCommands.VolumeFlag, $"{workDir}:{WorkMount}", image,
                CliCommands.Run, $"{WorkMount}/{FilePaths.BrokerProbeAlgoFile}",
                CliFlags.Ctid, request.CtidUsername,
                CliFlags.PwdFile, $"{WorkMount}/{FilePaths.CtidPwdFile}",
                CliFlags.Account, request.AccountNumber.ToString(CultureInfo.InvariantCulture)
            };

            var (timedOut, output) = await RunAsync(args, accounts.BrokerProbeTimeout, ct);
            return Interpret(request, timedOut, output);
        }
        catch (Exception ex)
        {
            log.BrokerProbeFailed(request.AccountNumber, ex.Message);
            return BrokerVerificationResult.Failed(BrokerVerificationError.ProbeFailed);
        }
        finally
        {
            try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { /* swallow */ }
        }
    }

    private BrokerVerificationResult Interpret(BrokerProbeRequest request, bool timedOut, string output)
    {
        foreach (var line in output.Split('\n'))
        {
            if (!BrokerProbeOutput.TryParseBroker(line, out var broker)) continue;
            log.BrokerProbeVerified(request.AccountNumber, broker.Value);
            return BrokerVerificationResult.Verified(broker);
        }

        if (BrokerProbeOutput.IndicatesLoginFailure(output))
        {
            log.BrokerProbeFailed(request.AccountNumber, "login failed");
            return BrokerVerificationResult.Failed(BrokerVerificationError.LoginFailed);
        }

        var error = timedOut ? BrokerVerificationError.Timeout : BrokerVerificationError.ProbeFailed;
        log.BrokerProbeFailed(request.AccountNumber, error.ToString());
        return BrokerVerificationResult.Failed(error);
    }

    private async Task<(bool timedOut, string output)> RunAsync(
        List<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(arguments[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        for (var i = 1; i < arguments.Count; i++) psi.ArgumentList.Add(arguments[i]);

        using var p = Process.Start(psi)!;
        var sb = new StringBuilder();
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) sb.AppendLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        using var timeoutCts = timeout == Timeout.InfiniteTimeSpan
            ? new CancellationTokenSource()
            : new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var timedOut = false;
        try
        {
            await p.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            timedOut = true;
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* swallow */ }
        }
        return (timedOut, sb.ToString());
    }
}
