using System.Text;
using System.Text.Json;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace Infrastructure.Ai.Providers;

/// <summary>
/// Built-in real local LLM running in-process via Microsoft.ML.OnnxRuntimeGenAI (e.g. Phi-3.5-mini). No
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
    private int _contextLength = AiConstants.BuiltInDefaultContextLength;

    public AiProviderKind Kind => AiProviderKind.BuiltInOnnx;

    /// <summary>True when the default model directory is present — cheap check, safe on the request path.</summary>
    public bool IsModelPresent() => IsModelPresent(null);

    /// <summary>True when the model directory for the given credential Model (a built-in submodel key, or the
    /// generic/default when null) is present and usable.</summary>
    public bool IsModelPresent(string? model)
    {
        var dir = ResolveModelDir(model);
        return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "genai_config.json"));
    }

    public async Task<AiResult> CompleteAsync(AiProviderRequest request, CancellationToken ct)
    {
        if (!IsModelPresent(request.Model)) return AiResult.Fail(NotPresentMessage(request.Model));

        var maxTokens = Math.Clamp(request.MaxTokens, 16, options.CurrentValue.Ai.BuiltIn.MaxTokens);
        try
        {
            return await Task.Run(() => Generate(request, maxTokens, request.Model, ct), ct);
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

    // The model is absent: when auto-download is enabled, kick off the one-time background install of the
    // requested built-in model and tell the caller it is downloading (a retryable, informative failure)
    // instead of the bare "not installed".
    private string NotPresentMessage(string? model)
    {
        if (installer is null || !options.CurrentValue.Ai.BuiltIn.AutoDownload)
            return AiConstants.BuiltInUnavailableMessage;

        var key = BuiltInModelCatalog.ForKey(model).Key;
        installer.EnsureInstalling(key);
        return installer.StateOf(key) == BuiltInModelInstallState.Failed
            ? AiConstants.BuiltInUnavailableMessage
            : AiConstants.BuiltInDownloadingMessage;
    }

    private AiResult Generate(AiProviderRequest request, int maxTokens, string? model, CancellationToken ct)
    {
        _gate.Wait(ct);
        try
        {
            if (!EnsureLoaded(model)) return AiResult.Fail(AiConstants.BuiltInUnavailableMessage);

            var prompt = BuildPrompt(request.System, request.User);
            using var sequences = _tokenizer!.Encode(prompt);
            // ONNX GenAI "max_length" is the TOTAL sequence budget (prompt + generated tokens), NOT the
            // number of tokens to generate. Setting it to just the output budget throws
            // "input sequence_length (N) is >= max_length (M)" whenever the prompt is longer than the
            // budget (e.g. the multi-line currency-strength prompt). Size it as prompt + output budget,
            // but never above the model's context window — ORT GenAI throws
            // "max_length cannot be greater than model context_length" otherwise. A large code-generation
            // prompt can exceed a small local model's window entirely; fail cleanly rather than crash.
            var promptTokens = sequences[0].Length;
            if (ComputeMaxLength(promptTokens, maxTokens, _contextLength) is not { } maxLength)
                return AiResult.Fail(AiConstants.BuiltInContextTooSmallMessage);
            using var generatorParams = new GeneratorParams(_model!);
            generatorParams.SetSearchOption("max_length", maxLength);
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

    private bool EnsureLoaded(string? model)
    {
        var dir = ResolveModelDir(model);
        if (_model is not null && _loadedPath == dir) return true;
        if (_loadFailed && _loadedPath == dir) return false;

        try
        {
            _model?.Dispose();
            _tokenizer?.Dispose();
            _model = new Model(dir);
            _tokenizer = new Tokenizer(_model);
            _contextLength = ReadContextLength(dir);
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

    private string ResolveModelDir(string? model)
    {
        var path = options.CurrentValue.Ai.BuiltIn.ModelPath;
        var root = Path.IsPathRooted(path) ? path : Path.Combine(AppContext.BaseDirectory, path);
        return SelectModelDir(root, model);
    }

    // Map a credential's Model to its on-disk directory: the generic/default/blank id (or an unknown /
    // not-yet-installed submodel) loads the ModelPath root; a submodel key whose ModelPath/<key> directory
    // is an installed model loads that sub-directory — so several built-in models coexist and are selectable.
    public static string SelectModelDir(string root, string? model)
    {
        if (string.IsNullOrWhiteSpace(model)
            || string.Equals(model, AiConstants.BuiltInModel, StringComparison.OrdinalIgnoreCase))
            return root;

        var sub = Path.Combine(root, model);
        return Directory.Exists(sub) && File.Exists(Path.Combine(sub, "genai_config.json")) ? sub : root;
    }

    // The total sequence budget (prompt + generated tokens) for ORT GenAI's "max_length", clamped to the
    // model's context window. Returns null when the prompt alone already fills/exceeds the window — the
    // caller then degrades to a typed failure instead of letting ORT GenAI throw
    // "max_length cannot be greater than model context_length" / "input sequence_length >= max_length".
    public static int? ComputeMaxLength(int promptTokens, int maxTokens, int contextLength) =>
        promptTokens >= contextLength ? null : Math.Min(promptTokens + maxTokens, contextLength);

    // The model's token context window, read from genai_config.json (model.context_length). Bounds
    // max_length so ORT GenAI never throws on a prompt that would exceed the window.
    private static int ReadContextLength(string dir)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "genai_config.json")));
            if (doc.RootElement.TryGetProperty("model", out var model)
                && model.TryGetProperty("context_length", out var ctx)
                && ctx.TryGetInt32(out var value) && value > 0)
                return value;
        }
        catch (Exception)
        {
            // Fall back to the conservative default when the config is absent/unreadable.
        }
        return AiConstants.BuiltInDefaultContextLength;
    }

    // Phi-3 / Phi-3.5 style chat template (the canonical bundled model). Harmless on other instruct models.
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
