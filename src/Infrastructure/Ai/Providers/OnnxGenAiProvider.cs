using System.Text;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Built-in real local LLM running in-process via Microsoft.ML.OnnxRuntimeGenAI (e.g. Phi-3-mini). No
/// API key, no external service — shipped with the app and enabled by default so every deployment has
/// working AI out of the box. The model (config + weights) lives in a directory (App:Ai:BuiltIn:ModelPath);
/// when it is absent the provider degrades to a typed failure with an install hint (never throws). The
/// model + tokenizer load lazily once and are reused; generation is serialised (a single model instance
/// is not run concurrently).
/// </summary>
public sealed class OnnxGenAiProvider(
    IOptionsMonitor<AppOptions> options, ILogger<OnnxGenAiProvider> logger,
    IBuiltInModelInstaller? installer = null) : IAiProvider, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Model? _model;
    private Tokenizer? _tokenizer;
    private string? _loadedPath;
    private bool _loadFailed;

    public AiProviderKind Kind => AiProviderKind.BuiltInOnnx;

    /// <summary>True when a usable model directory is present — cheap check, safe to call on the request path.</summary>
    public bool IsModelPresent()
    {
        var dir = ResolveModelDir();
        return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "genai_config.json"));
    }

    public async Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        if (!IsModelPresent()) return AiResult.Fail(NotPresentMessage());

        var maxTokens = Math.Clamp(request.MaxTokens, 16, options.CurrentValue.Ai.BuiltIn.MaxTokens);
        try
        {
            return await Task.Run(() => Generate(request, maxTokens, ct), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.AiRequestError(ex);
            return AiResult.Fail("Built-in AI request errored.");
        }
    }

    // The model is absent: when auto-download is enabled, kick off the one-time background install and tell
    // the caller it is downloading (a retryable, informative failure) instead of the bare "not installed".
    private string NotPresentMessage()
    {
        if (installer is null || !options.CurrentValue.Ai.BuiltIn.AutoDownload)
            return AiConstants.BuiltInUnavailableMessage;

        installer.EnsureInstalling();
        return installer.State == BuiltInModelInstallState.Failed
            ? AiConstants.BuiltInUnavailableMessage
            : AiConstants.BuiltInDownloadingMessage;
    }

    private AiResult Generate(AiProviderRequest request, int maxTokens, CancellationToken ct)
    {
        _gate.Wait(ct);
        try
        {
            if (!EnsureLoaded()) return AiResult.Fail(AiConstants.BuiltInUnavailableMessage);

            var prompt = BuildPrompt(request.System, request.User);
            using var sequences = _tokenizer!.Encode(prompt);
            using var generatorParams = new GeneratorParams(_model!);
            generatorParams.SetSearchOption("max_length", maxTokens);
            generatorParams.SetInputSequences(sequences);

            using var generator = new Generator(_model!, generatorParams);
            using var stream = _tokenizer.CreateStream();
            var sb = new StringBuilder();
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.ComputeLogits();
                generator.GenerateNextToken();
                var sequence = generator.GetSequence(0);
                sb.Append(stream.Decode(sequence[sequence.Length - 1]));
            }

            var text = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(text)
                ? AiResult.Fail("Built-in AI returned an empty response.")
                : AiResult.Ok(text);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool EnsureLoaded()
    {
        var dir = ResolveModelDir();
        if (_model is not null && _loadedPath == dir) return true;
        if (_loadFailed && _loadedPath == dir) return false;

        try
        {
            _model?.Dispose();
            _tokenizer?.Dispose();
            _model = new Model(dir);
            _tokenizer = new Tokenizer(_model);
            _loadedPath = dir;
            _loadFailed = false;
            return true;
        }
        catch (Exception ex)
        {
            logger.AiRequestError(ex);
            _model = null;
            _tokenizer = null;
            _loadedPath = dir;
            _loadFailed = true;
            return false;
        }
    }

    private string ResolveModelDir()
    {
        var path = options.CurrentValue.Ai.BuiltIn.ModelPath;
        return Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
    }

    // Phi-3 style chat template (the canonical bundled model). Harmless on other instruct models.
    private static string BuildPrompt(string system, string user)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(system)) sb.Append("<|system|>\n").Append(system).Append("<|end|>\n");
        sb.Append("<|user|>\n").Append(user).Append("<|end|>\n<|assistant|>\n");
        return sb.ToString();
    }

    public void Dispose()
    {
        _tokenizer?.Dispose();
        _model?.Dispose();
        _gate.Dispose();
    }
}
