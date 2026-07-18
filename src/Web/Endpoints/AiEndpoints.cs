using System.Text;
using System.Text.Json;
using Core;
using Core.Ai;
using Core.Constants;
using Infrastructure.Builder;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ai").RequireAuthorization("UserOrAbove")
            .RequireFeature(Core.Features.FeatureFlag.Ai);

        // Status keeps its { enabled } shape (the gate JS/E2E depend on it) and adds the active
        // provider kind + model for display when configured.
        g.MapGet("/status", (IAiProviderStore store) =>
        {
            var active = store.Active;
            return Results.Ok(new
            {
                enabled = active is not null,
                kind = active?.Kind.ToString(),
                model = active?.Model
            });
        });

        g.MapGet("/providers", async (IAiProviderStore store, CancellationToken ct) =>
            Results.Ok(await store.ListAsync(ct))).RequireAuthorization(AuthPolicies.Owner);

        g.MapPut("/providers", async (UpsertProviderRequest req, IAiProviderStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.BaseUrl)) return Results.BadRequest("Base URL is required");
            if (string.IsNullOrWhiteSpace(req.Model)) return Results.BadRequest("Model is required");
            try
            {
                var caps = req.Capabilities is { } c
                    ? new Core.Ai.AiProviderCapabilities(c.SupportsWebSearch, c.SupportsVision, c.SupportsSystemRole, c.SupportsTools)
                    : (Core.Ai.AiProviderCapabilities?)null;
                var id = await store.UpsertAsync(new Core.Ai.UpsertAiProviderCommand(
                    req.Id, req.Kind, req.BaseUrl!, req.Model!, req.ApiKey, req.MaxTokens, caps, req.Activate), ct);
                return Results.Ok(new { id });
            }
            catch (Core.Domain.DomainException ex)
            {
                return Results.BadRequest(ex.Code);
            }
        }).RequireAuthorization(AuthPolicies.Owner);

        g.MapPost("/providers/{id:guid}/activate", async (Guid id, IAiProviderStore store, CancellationToken ct) =>
        {
            await store.ActivateAsync(id, ct);
            return Results.Ok(new { enabled = store.HasActive });
        }).RequireAuthorization(AuthPolicies.Owner);

        g.MapDelete("/providers/{id:guid}", async (Guid id, IAiProviderStore store, CancellationToken ct) =>
        {
            await store.RemoveAsync(id, ct);
            return Results.Ok(new { enabled = store.HasActive });
        }).RequireAuthorization(AuthPolicies.Owner);

        // Built-in ONNX local model install state + a manual trigger, so the owner can see whether the
        // model is present/downloading/failed and kick the one-time background download from the UI.
        g.MapGet("/built-in/status", (IBuiltInModelInstaller installer) =>
            Results.Ok(new { installed = installer.IsInstalled(), state = installer.State.ToString() }))
            .RequireAuthorization(AuthPolicies.Owner);

        g.MapPost("/built-in/install", (IBuiltInModelInstaller installer) =>
        {
            installer.EnsureInstalling();
            return Results.Ok(new { installed = installer.IsInstalled(), state = installer.State.ToString() });
        }).RequireAuthorization(AuthPolicies.Owner);

        // Browse the models an endpoint advertises so the user picks one instead of hand-typing a model id.
        // Probes an UNSAVED endpoint (kind + base URL + optional key from the dialog) — the built-in kind
        // ignores base URL/key and enumerates installed local model directories. Any signed-in user may
        // probe (UserOrAbove, inherited) since a user adds their own provider. Never throws — degrades to [].
        g.MapPost("/models/probe", async (ProbeModelsRequest req, IAiModelCatalog catalog, CancellationToken ct) =>
        {
            try
            {
                var baseUrl = req.Kind is AiProviderKind.BuiltInOnnx or AiProviderKind.Demo
                              || string.IsNullOrWhiteSpace(req.BaseUrl)
                    ? new AiEndpoint(AiConstants.BuiltInBaseUrl)
                    : new AiEndpoint(req.BaseUrl!);
                var models = await catalog.ListModelsAsync(req.Kind, baseUrl, req.ApiKey, ct);
                return Results.Ok(models);
            }
            catch (Core.Domain.DomainException ex)
            {
                return Results.BadRequest(ex.Code);
            }
        });

        // Ping the active provider with a tiny completion and report success + latency — handy for
        // verifying a local endpoint after adding it.
        g.MapPost("/providers/test", async (IAiClient client, CancellationToken ct) =>
        {
            if (!client.Enabled) return Results.Ok(new { success = false, latencyMs = 0L, error = AiConstants.DisabledMessage });
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await client.CompleteAsync(
                new AiTextRequest("You are a connectivity probe. Reply with the single word: ok.", "ping", MaxTokens: 16), ct);
            sw.Stop();
            return Results.Ok(new { success = result.Success, latencyMs = sw.ElapsedMilliseconds, error = result.Error });
        }).RequireAuthorization(AuthPolicies.Owner);

        // Per-user providers: any signed-in user may add their own AI provider, which overrides the
        // deployment default for their own AI features (UserOrAbove — inherited from the group).
        g.MapGet("/my-providers", async (IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
            u.UserId is { } uid ? Results.Ok(await store.ListForUserAsync(uid, ct)) : Results.Unauthorized());

        g.MapPut("/my-providers", async (UpsertProviderRequest req, IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(req.BaseUrl)) return Results.BadRequest("Base URL is required");
            if (string.IsNullOrWhiteSpace(req.Model)) return Results.BadRequest("Model is required");
            try
            {
                var caps = req.Capabilities is { } c
                    ? new Core.Ai.AiProviderCapabilities(c.SupportsWebSearch, c.SupportsVision, c.SupportsSystemRole, c.SupportsTools)
                    : (Core.Ai.AiProviderCapabilities?)null;
                var id = await store.UpsertForUserAsync(uid, new Core.Ai.UpsertAiProviderCommand(
                    req.Id, req.Kind, req.BaseUrl!, req.Model!, req.ApiKey, req.MaxTokens, caps, req.Activate), ct);
                return Results.Ok(new { id });
            }
            catch (Core.Domain.DomainException ex)
            {
                return Results.BadRequest(ex.Code);
            }
        });

        g.MapPost("/my-providers/{id:guid}/activate", async (Guid id, IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            await store.ActivateForUserAsync(uid, id, ct);
            return Results.Ok(new { enabled = store.HasActive });
        });

        g.MapDelete("/my-providers/{id:guid}", async (Guid id, IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            await store.RemoveForUserAsync(uid, id, ct);
            return Results.Ok(new { enabled = store.HasActive });
        });

        g.MapPost("/generate", async (GenerateCBotRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.GenerateCBotAsync(req.Language ?? "CSharp", req.Description ?? "", ct)));

        g.MapPost("/review", async (ReviewCBotRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.ReviewCBotAsync(req.Language ?? "CSharp", req.Source ?? "", ct)));

        g.MapPost("/debate", async (DebateRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.DebateStrategyAsync(req.Name ?? "cBot", req.Language ?? "CSharp", req.Source ?? "", AiConstants.DebateMaxTokens, ct)));

        g.MapPost("/sentiment", async (SentimentRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.MarketSentimentAsync(req.Symbol ?? "", ct)));

        g.MapPost("/vision", async (VisionRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.VisionToStrategyAsync(
                new AiImage(req.MediaType ?? "image/png", req.Base64 ?? ""), req.Note, ct)));

        g.MapPost("/curate", async (CurateRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.CurateStrategyAsync(req.Name ?? "cBot", req.Language ?? "CSharp", req.Source ?? "", ct)));

        g.MapPost("/recommend-copy-profile", async (RecommendCopyProfileRequest req, IAiFeatureService ai, CancellationToken ct) =>
            Results.Ok(await ai.RecommendCopyProfileAsync(req.RiskProfile ?? "", req.SourceDescription ?? "", ct)));

        g.MapPost("/analyze-backtest/{id:guid}", async (
            Guid id, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var bt = await db.Instances.OfType<CompletedBacktestInstance>()
                .Where(i => i.Id == iid && i.UserId == uid)
                .Select(i => new { i.ReportJson, Name = i.CBot.Name })
                .FirstOrDefaultAsync(ct);
            if (bt is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(bt.ReportJson)) return Results.BadRequest("no backtest report available");
            return Results.Ok(await ai.AnalyzeBacktestAsync(bt.Name, bt.ReportJson!, ct));
        });

        g.MapPost("/optimize-params/{cbotId:guid}", async (
            Guid cbotId, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var cid = CBotId.From(cbotId);
            var name = await db.CBots.Where(c => c.Id == cid && c.UserId == uid)
                .Select(c => c.Name).FirstOrDefaultAsync(ct);
            if (name is null) return Results.NotFound();
            var current = await db.ParamSets.Where(p => p.CBotId == cid && p.UserId == uid)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.JsonContent).FirstOrDefaultAsync(ct) ?? "{}";
            return Results.Ok(await ai.ProposeParamSetsAsync(name, current, null, ct));
        });

        g.MapPost("/exposure-check", async (
            DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var live = await db.Instances.OfType<RunningRunInstance>()
                .Where(i => i.UserId == uid && i.Symbol != null)
                .OrderByDescending(i => i.CreatedAt)
                .Take(AiConstants.ExposureMaxInstances)
                .Select(i => new AiInstanceContext(i.CBot.Name, "Run", "Running", i.Symbol, i.Timeframe, null))
                .ToListAsync(ct);
            if (live.Count == 0)
                return Results.Ok(new AiResult(false, string.Empty, "no live positions to check"));
            return Results.Ok(await ai.AssessLiveExposureAsync(live, AiConstants.ExposureMaxTokens, ct));
        });

        g.MapPost("/portfolio-digest", async (
            DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var instances = await db.Instances.Include(i => i.CBot)
                .Where(i => i.UserId == uid)
                .OrderByDescending(i => i.CreatedAt)
                .Take(AiConstants.DigestMaxInstances)
                .ToListAsync(ct);
            if (instances.Count == 0)
                return Results.Ok(new AiResult(false, string.Empty, "no instances to analyze yet"));

            var portfolio = instances
                .Select(i => new AiInstanceContext(i.CBot.Name, i.KindName, i.StatusName, i.Symbol, i.Timeframe, DetailOf(i)))
                .ToList();
            return Results.Ok(await ai.PortfolioDigestAsync(portfolio, AiConstants.DigestMaxTokens, ct));
        });

        g.MapPost("/tune-advice/{cbotId:guid}", async (
            Guid cbotId, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var cid = CBotId.From(cbotId);
            var name = await db.CBots.Where(c => c.Id == cid && c.UserId == uid)
                .Select(c => c.Name).FirstOrDefaultAsync(ct);
            if (name is null) return Results.NotFound();

            var reports = await db.Instances.OfType<CompletedBacktestInstance>()
                .Where(i => i.CBotId == cid && i.UserId == uid && i.ReportJson != null)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => i.ReportJson!)
                .Take(2).ToListAsync(ct);
            if (reports.Count == 0) return Results.Ok(new AiResult(false, string.Empty, "no completed backtests to analyze yet"));

            var current = await db.ParamSets.Where(p => p.CBotId == cid && p.UserId == uid)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.JsonContent).FirstOrDefaultAsync(ct) ?? "{}";
            var previous = reports.Count > 1 ? reports[1] : null;
            return Results.Ok(await ai.AssessStrategyDecayAsync(name, previous, reports[0], current, AiConstants.TuneAdviceMaxTokens, ct));
        });

        g.MapPost("/post-mortem/{id:guid}", async (
            Guid id, DataContext db, ICurrentUser u, IAiFeatureService ai, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var iid = InstanceId.From(id);
            var instance = await db.Instances.Include(i => i.CBot)
                .FirstOrDefaultAsync(i => i.Id == iid && i.UserId == uid, ct);
            if (instance is null) return Results.NotFound();
            var context = new AiInstanceContext(
                instance.CBot.Name, instance.KindName, instance.StatusName,
                instance.Symbol, instance.Timeframe, DetailOf(instance));
            return Results.Ok(await ai.PostMortemAsync(context, ct));
        });

        // Feature A: generate a full buildable source project, build it, and self-repair on failure.
        g.MapPost("/generate-project", async (
            GenerateProjectRequest req, DataContext db, ICurrentUser u, IAiFeatureService ai,
            ISecretProtector protector, CBotBuilder builder, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (!ai.Enabled) return Results.Ok(new { success = false, error = AiConstants.DisabledMessage });

            var language = req.Language ?? "CSharp";
            var name = string.IsNullOrWhiteSpace(req.Name) ? "AiBot" : req.Name!;
            CBotSourceProject project = language.Equals("Python", StringComparison.OrdinalIgnoreCase)
                ? PythonProject.Create(uid, name) : CSharpProject.Create(uid, name);

            var files = JsonSerializer.Deserialize<Dictionary<string, string>>(
                Templates.CreateProjectJson(project.LanguageName, name)) ?? new Dictionary<string, string>();
            var codeKey = files.Keys.FirstOrDefault(k => k.EndsWith(project.FileExtension, StringComparison.Ordinal));
            if (codeKey is null) return Results.Ok(new { success = false, error = "project template has no code file" });

            var gen = await ai.GenerateCBotAsync(language, req.Description ?? "", ct);
            if (!gen.Success) return Results.Ok(new { success = false, error = gen.Error });
            files[codeKey] = ExtractCode(gen.Text);

            db.CBotSourceProjects.Add(project);

            const int maxAttempts = 3;
            var success = false;
            var log = string.Empty;
            var attempts = 0;
            for (attempts = 1; attempts <= maxAttempts; attempts++)
            {
                project.SetFiles(protector.Protect(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files)), EncryptionPurposes.CbotSource));
                await db.SaveChangesAsync(ct);

                var build = await builder.BuildAsync(project, uid, ct);
                success = build.Success;
                log = build.Log;
                if (success || attempts == maxAttempts) break;

                var fix = await ai.FixCBotAsync(language, files[codeKey], build.Log, ct);
                if (!fix.Success) break;
                files[codeKey] = ExtractCode(fix.Text);
            }

            return Results.Ok(new { success, projectId = project.Id.Value, attempts, log = ClipLog(log) });
        });

        // Feature C: plain-English intent -> generate -> build/self-repair -> create a runnable cBot.
        g.MapPost("/build-strategy", async (
            BuildStrategyRequest req, DataContext db, ICurrentUser u, IAiFeatureService ai,
            ISecretProtector protector, CBotBuilder builder, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (!ai.Enabled) return Results.Ok(new { success = false, error = AiConstants.DisabledMessage });
            if (string.IsNullOrWhiteSpace(req.Description))
                return Results.Ok(new { success = false, error = "describe the strategy first" });

            var language = req.Language ?? "CSharp";
            var name = string.IsNullOrWhiteSpace(req.Name) ? "AiBot" : req.Name!.Trim();
            var projectName = await UniqueNameAsync(
                db.CBotSourceProjects.Where(p => p.UserId == uid).Select(p => p.Name), name, ct);
            CBotSourceProject project = language.Equals("Python", StringComparison.OrdinalIgnoreCase)
                ? PythonProject.Create(uid, projectName) : CSharpProject.Create(uid, projectName);

            var files = JsonSerializer.Deserialize<Dictionary<string, string>>(
                Templates.CreateProjectJson(project.LanguageName, project.Name)) ?? new Dictionary<string, string>();
            var codeKey = files.Keys.FirstOrDefault(k => k.EndsWith(project.FileExtension, StringComparison.Ordinal));
            if (codeKey is null) return Results.Ok(new { success = false, error = "project template has no code file" });

            var gen = await ai.GenerateCBotAsync(language, req.Description!, ct);
            if (!gen.Success) return Results.Ok(new { success = false, error = gen.Error });
            files[codeKey] = ExtractCode(gen.Text);

            db.CBotSourceProjects.Add(project);

            try
            {
            const int maxAttempts = 3;
            var success = false;
            var log = string.Empty;
            byte[]? algo = null;
            var attempts = 0;
            for (attempts = 1; attempts <= maxAttempts; attempts++)
            {
                project.SetFiles(protector.Protect(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files)), EncryptionPurposes.CbotSource));
                await db.SaveChangesAsync(ct);

                var build = await builder.BuildAsync(project, uid, ct);
                success = build.Success;
                log = build.Log;
                algo = build.AlgoBytes;
                if (success || attempts == maxAttempts) break;

                var fix = await ai.FixCBotAsync(language, files[codeKey], build.Log, ct);
                if (!fix.Success) break;
                files[codeKey] = ExtractCode(fix.Text);
            }

            if (!success || algo is null)
                return Results.Ok(new { success = false, projectId = project.Id.Value, attempts, log = ClipLog(log), code = files[codeKey], language });

            var cbotName = await UniqueNameAsync(db.CBots.Where(c => c.UserId == uid).Select(c => c.Name), project.Name, ct);
            var cbot = CBot.Create(uid, cbotName, protector.Protect(algo, EncryptionPurposes.CbotAlgo), project.Id);
            db.CBots.Add(cbot);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { success = true, projectId = project.Id.Value, cbotId = cbot.Id.Value, attempts, log = ClipLog(log), code = files[codeKey], language });
            }
            catch (DbUpdateException)
            {
                return Results.Ok(new { success = false, error = "a cBot or project with that name already exists — try another name" });
            }
        });

        // Feature B: closed optimization loop — AI proposes parameter sets, we backtest each across nodes.
        g.MapPost("/optimize-run/{cbotId:guid}", async (
            Guid cbotId, OptimizeRunRequest req, DataContext db, ICurrentUser u, IAiFeatureService ai,
            ISecretProtector protector, INodeScheduler scheduler, IContainerDispatcherFactory factory,
            TimeProvider timeProvider, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (!ai.Enabled) return Results.Ok(new { success = false, error = AiConstants.DisabledMessage });

            var cid = CBotId.From(cbotId);
            var cbot = await db.CBots.FirstOrDefaultAsync(c => c.Id == cid && c.UserId == uid, ct);
            if (cbot is null) return Results.NotFound();
            var accountId = TradingAccountId.From(req.TradingAccountId);
            var account = await db.TradingAccounts.Include(t => t.CTid)
                .FirstOrDefaultAsync(t => t.Id == accountId && t.CTid.UserId == uid, ct);
            if (account is null) return Results.BadRequest("trading account not found");

            var current = await db.ParamSets.Where(p => p.CBotId == cid && p.UserId == uid)
                .OrderByDescending(p => p.CreatedAt).Select(p => p.JsonContent).FirstOrDefaultAsync(ct) ?? "{}";

            var count = Math.Clamp(req.Count ?? 3, 1, 5);
            var suite = await ai.ProposeParamSetSuiteAsync(cbot.Name, current, count, ct);
            if (!suite.Success) return Results.Ok(new { success = false, error = suite.Error });

            var proposals = ParseParamSuite(suite.Text, count);
            if (proposals.Count == 0)
                return Results.Ok(new { success = false, error = "AI returned no usable parameter sets", raw = ClipLog(suite.Text) });

            var imageTag = string.IsNullOrWhiteSpace(req.DockerImageTag) ? "latest" : req.DockerImageTag!;
            var symbol = string.IsNullOrWhiteSpace(req.Symbol) ? "EURUSD" : req.Symbol!;
            var timeframe = string.IsNullOrWhiteSpace(req.Timeframe) ? "h1" : req.Timeframe!;
            var algo = protector.Unprotect(cbot.EncryptedAlgo, EncryptionPurposes.CbotAlgo);

            var imageTagValue = new DockerImageTag(imageTag);
            var symbolValue = new Symbol(symbol);
            var timeframeValue = new Timeframe(timeframe);
            var launched = new List<object>();
            foreach (var (name, json) in proposals)
            {
                var paramSet = ParamSet.Create(uid, cid, name, json);
                db.ParamSets.Add(paramSet);
                await db.SaveChangesAsync(ct);

                var node = await scheduler.PickNodeAsync("Backtest", ct);
                if (node is null)
                {
                    launched.Add(new { name, paramSetId = paramSet.Id.Value, instanceId = (Guid?)null, error = "no node available" });
                    continue;
                }

                var starting = BacktestInstance.CreateStarting(uid, cid, node.Id, imageTagValue, symbolValue,
                    timeframeValue, req.BacktestSettingsJson, accountId, paramSet.Id);
                db.Instances.Add(starting);
                await db.SaveChangesAsync(ct);
                starting.AttachNode(node);

                try
                {
                    var containerId = await factory.For(node).StartAsync(starting, algo, json, ct);
                    var running = starting.ToRunning(containerId, timeProvider.GetUtcNow());
                    db.Instances.Remove(starting);
                    db.Instances.Add(running);
                    await db.SaveChangesAsync(ct);
                    launched.Add(new { name, paramSetId = paramSet.Id.Value, instanceId = (Guid?)running.Id.Value, error = (string?)null });
                }
                catch (Exception ex)
                {
                    var failed = starting.ToFailed(ex.Message, timeProvider.GetUtcNow());
                    db.Instances.Remove(starting);
                    db.Instances.Add(failed);
                    await db.SaveChangesAsync(ct);
                    launched.Add(new { name, paramSetId = paramSet.Id.Value, instanceId = (Guid?)null, error = ex.Message });
                }
            }

            return Results.Ok(new { success = true, launched });
        });

        return app;
    }

    private const int MaxLogChars = 4000;

    private static string ClipLog(string value) => value.Length <= MaxLogChars ? value : value[^MaxLogChars..];

    private static async Task<string> UniqueNameAsync(IQueryable<string> existingNames, string desired, CancellationToken ct)
    {
        var taken = await existingNames.ToListAsync(ct);
        var set = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        if (!set.Contains(desired)) return desired;
        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{desired} {suffix}";
            if (!set.Contains(candidate)) return candidate;
        }
        return $"{desired} {Guid.NewGuid():N}";
    }

    private static string ExtractCode(string text)
    {
        var trimmed = text.Trim();
        var fence = trimmed.IndexOf("```", StringComparison.Ordinal);
        if (fence < 0) return trimmed;
        var start = trimmed.IndexOf('\n', fence);
        if (start < 0) return trimmed;
        var end = trimmed.IndexOf("```", start, StringComparison.Ordinal);
        return end < 0 ? trimmed[(start + 1)..].Trim() : trimmed[(start + 1)..end].Trim();
    }

    private static List<(string Name, string Json)> ParseParamSuite(string text, int max)
    {
        var results = new List<(string, string)>();
        foreach (var candidate in new[] { StripFences(text), BracketSlice(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                var index = 0;
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (index >= max) break;
                    var name = element.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String
                        ? n.GetString()!
                        : $"AI-opt {index + 1}";
                    var paramsElement = element.TryGetProperty("parameters", out var p) ? p : element;
                    results.Add((name, paramsElement.GetRawText()));
                    index++;
                }
                if (results.Count > 0) return results;
            }
            catch (JsonException) { /* try next candidate */ }
        }
        return results;
    }

    private static string StripFences(string value)
    {
        var s = value.Trim();
        if (!s.StartsWith("```", StringComparison.Ordinal)) return s;
        var nl = s.IndexOf('\n');
        if (nl >= 0) s = s[(nl + 1)..];
        if (s.EndsWith("```", StringComparison.Ordinal)) s = s[..^3];
        return s.Trim();
    }

    private static string BracketSlice(string value)
    {
        var open = value.IndexOf('[');
        var close = value.LastIndexOf(']');
        return open >= 0 && close > open ? value[open..(close + 1)] : string.Empty;
    }

    private static string? DetailOf(Instance instance) => instance switch
    {
        FailedRunInstance f => $"Failure: {f.FailureReason}",
        FailedBacktestInstance f => $"Failure: {f.FailureReason}",
        CompletedBacktestInstance c => c.ReportJson is null ? null : $"Report JSON:\n{c.ReportJson}",
        _ => null
    };
}

public sealed record UpsertProviderRequest(
    Guid? Id, Core.Ai.AiProviderKind Kind, string? BaseUrl, string? Model, string? ApiKey,
    int? MaxTokens, CapabilitiesRequest? Capabilities, bool Activate);
public sealed record CapabilitiesRequest(
    bool SupportsWebSearch, bool SupportsVision, bool SupportsSystemRole, bool SupportsTools);
public sealed record ProbeModelsRequest(Core.Ai.AiProviderKind Kind, string? BaseUrl, string? ApiKey);
public sealed record GenerateCBotRequest(string? Language, string? Description);
public sealed record ReviewCBotRequest(string? Language, string? Source);
public sealed record DebateRequest(string? Name, string? Language, string? Source);
public sealed record SentimentRequest(string? Symbol);
public sealed record VisionRequest(string? MediaType, string? Base64, string? Note);
public sealed record CurateRequest(string? Name, string? Language, string? Source);
public sealed record RecommendCopyProfileRequest(string? RiskProfile, string? SourceDescription);
public sealed record GenerateProjectRequest(string? Name, string? Language, string? Description);
public sealed record BuildStrategyRequest(string? Name, string? Language, string? Description);
public sealed record OptimizeRunRequest(
    Guid TradingAccountId, string? Symbol, string? Timeframe,
    string? DockerImageTag, string? BacktestSettingsJson, int? Count);
