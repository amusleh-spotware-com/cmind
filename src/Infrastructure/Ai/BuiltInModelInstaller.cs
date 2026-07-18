using System.Collections.Concurrent;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai;

/// <summary>
/// Downloads a built-in ONNX model once, in the background, so the built-in local LLM works out of the box
/// with no manual provisioning. Handles the whole curated <see cref="BuiltInModelCatalog"/>: the default
/// model installs at the configured <c>ModelPath</c> root (the auto-download target); every other curated
/// model installs under <c>ModelPath/&lt;key&gt;</c> so several can coexist and the user can switch between
/// them. Single-flight per model: the first request that finds a model absent kicks off one download;
/// concurrent requests observe the in-progress state and degrade to a typed "downloading" failure. Files are
/// staged into a temp directory and atomically promoted so a partial/failed download never looks installed.
/// Uses a dedicated long-timeout HttpClient (model weights are large).
/// </summary>
public sealed class BuiltInModelInstaller(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<BuiltInModelInstaller> logger) : IBuiltInModelInstaller
{
    public const string HttpClientName = "builtin-model-download";

    private sealed class ModelDownload
    {
        public Task? Task { get; set; }
        public volatile BuiltInModelInstallState State = BuiltInModelInstallState.NotStarted;
    }

    private readonly Lock _gate = new();
    private readonly ConcurrentDictionary<string, ModelDownload> _downloads = new(StringComparer.OrdinalIgnoreCase);

    public bool IsInstalled() => IsInstalled(BuiltInModelCatalog.Default.Key);

    public BuiltInModelInstallState State => StateOf(BuiltInModelCatalog.Default.Key);

    public void EnsureInstalling() => EnsureInstalling(BuiltInModelCatalog.Default.Key);

    public bool IsInstalled(string key)
    {
        var dir = ResolveModelDir(BuiltInModelCatalog.ForKey(key));
        return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "genai_config.json"));
    }

    public BuiltInModelInstallState StateOf(string key)
    {
        if (IsInstalled(key)) return BuiltInModelInstallState.Installed;
        return _downloads.TryGetValue(BuiltInModelCatalog.ForKey(key).Key, out var d)
            ? d.State
            : BuiltInModelInstallState.NotStarted;
    }

    public IReadOnlyList<BuiltInModelStatus> Catalog() =>
        BuiltInModelCatalog.All
            .Select(spec => new BuiltInModelStatus(spec, IsInstalled(spec.Key), StateOf(spec.Key)))
            .ToList();

    public void EnsureInstalling(string key)
    {
        var spec = BuiltInModelCatalog.ForKey(key);
        if (IsInstalled(spec.Key) || !options.CurrentValue.Ai.BuiltIn.AutoDownload) return;

        lock (_gate)
        {
            var handle = _downloads.GetOrAdd(spec.Key, _ => new ModelDownload());
            if (handle.Task is { IsCompleted: false }) return;
            if (IsInstalled(spec.Key)) return;
            handle.State = BuiltInModelInstallState.Downloading;
            handle.Task = Task.Run(() => DownloadAsync(spec, handle));
        }
    }

    private async Task DownloadAsync(BuiltInModelSpec spec, ModelDownload handle)
    {
        var dir = ResolveModelDir(spec);
        var staging = dir + ".downloading";
        var (baseUrl, files) = ResolveSource(spec);

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

            handle.State = BuiltInModelInstallState.Installed;
            logger.BuiltInModelDownloadCompleted(downloaded);
        }
        catch (Exception ex)
        {
            handle.State = BuiltInModelInstallState.Failed;
            logger.BuiltInModelDownloadFailed(ex);
            TryDelete(staging);
        }
    }

    // The default model honours the deployment override (App:Ai:BuiltIn:DownloadBaseUrl/DownloadFiles) so an
    // ops team can repoint or air-gap it; every other curated model uses the catalog spec's own source.
    private (string BaseUrl, IReadOnlyList<string> Files) ResolveSource(BuiltInModelSpec spec)
    {
        if (spec.IsDefault)
        {
            var builtIn = options.CurrentValue.Ai.BuiltIn;
            var url = builtIn.DownloadBaseUrl.EndsWith('/') ? builtIn.DownloadBaseUrl : builtIn.DownloadBaseUrl + "/";
            var files = builtIn.DownloadFiles.Count > 0 ? builtIn.DownloadFiles : AiConstants.BuiltInModelDownloadFiles;
            return (url, files);
        }
        var baseUrl = spec.DownloadBaseUrl.EndsWith('/') ? spec.DownloadBaseUrl : spec.DownloadBaseUrl + "/";
        return (baseUrl, spec.Files);
    }

    private static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* swallow */ }
    }

    // The default model lives at the ModelPath root (back-compat + auto-download target); every other curated
    // model lives in its own ModelPath/<key> sub-directory so multiple built-in models coexist.
    private string ResolveModelDir(BuiltInModelSpec spec)
    {
        var path = options.CurrentValue.Ai.BuiltIn.ModelPath;
        var root = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        return spec.IsDefault ? root : Path.Combine(root, spec.Key);
    }
}
