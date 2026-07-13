using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

/// <summary>
/// Downloads the built-in ONNX model once, in the background, so the built-in local LLM works out of the
/// box with no manual provisioning. Single-flight: the first request that finds the model absent kicks off
/// one download; concurrent requests observe the in-progress state and degrade to a typed "downloading"
/// failure. Files are staged into a temp directory and atomically promoted so a partial/failed download
/// never looks installed. Uses a dedicated long-timeout HttpClient (model weights are large).
/// </summary>
public sealed class BuiltInModelInstaller(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<BuiltInModelInstaller> logger) : IBuiltInModelInstaller
{
    public const string HttpClientName = "builtin-model-download";

    private readonly Lock _gate = new();
    private Task? _download;
    private volatile BuiltInModelInstallState _state = BuiltInModelInstallState.NotStarted;

    public BuiltInModelInstallState State => IsInstalled() ? BuiltInModelInstallState.Installed : _state;

    public bool IsInstalled()
    {
        var dir = ResolveModelDir();
        return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "genai_config.json"));
    }

    public void EnsureInstalling()
    {
        if (IsInstalled() || !options.CurrentValue.Ai.BuiltIn.AutoDownload) return;

        lock (_gate)
        {
            if (_download is { IsCompleted: false }) return;
            if (IsInstalled()) return;
            _state = BuiltInModelInstallState.Downloading;
            _download = Task.Run(DownloadAsync);
        }
    }

    private async Task DownloadAsync()
    {
        var dir = ResolveModelDir();
        var staging = dir + ".downloading";
        var builtIn = options.CurrentValue.Ai.BuiltIn;
        var baseUrl = builtIn.DownloadBaseUrl.EndsWith('/') ? builtIn.DownloadBaseUrl : builtIn.DownloadBaseUrl + "/";
        var files = builtIn.DownloadFiles.Count > 0 ? builtIn.DownloadFiles : AiConstants.BuiltInModelDownloadFiles;

        try
        {
            logger.BuiltInModelDownloadStarted(dir);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            Directory.CreateDirectory(staging);

            var client = httpClientFactory.CreateClient(HttpClientName);
            var downloaded = 0;
            foreach (var file in files)
            {
                using var response = await client.GetAsync(
                    baseUrl + file, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None);
                // Optional files (e.g. added_tokens.json) may not exist for every model — skip a 404.
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) continue;
                response.EnsureSuccessStatusCode();

                var target = Path.Combine(staging, file);
                await using var source = await response.Content.ReadAsStreamAsync(CancellationToken.None);
                await using var sink = File.Create(target);
                await source.CopyToAsync(sink, CancellationToken.None);
                downloaded++;
            }

            if (!File.Exists(Path.Combine(staging, "genai_config.json")))
                throw new InvalidOperationException("Downloaded model is missing genai_config.json.");

            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(dir) ?? ".");
            Directory.Move(staging, dir);

            _state = BuiltInModelInstallState.Installed;
            logger.BuiltInModelDownloadCompleted(downloaded);
        }
        catch (Exception ex)
        {
            _state = BuiltInModelInstallState.Failed;
            logger.BuiltInModelDownloadFailed(ex);
            TryDelete(staging);
        }
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* swallow */ }
    }

    private string ResolveModelDir()
    {
        var path = options.CurrentValue.Ai.BuiltIn.ModelPath;
        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }
}
