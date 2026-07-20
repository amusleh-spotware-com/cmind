using System.Text;
using System.Text.Json;
using Core;
using Core.Ai;
using Core.Constants;
using Core.Logging;
using Core.Options;
using Infrastructure.Ai;
using Infrastructure.Builder;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Endpoints;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ai").RequireAuthorization("UserOrAbove")
            .RequireFeature(Core.Features.FeatureFlag.Ai);

        g.WithAiModelOverride();

        // Status keeps its { enabled } shape (the gate JS/E2E depend on it) and adds the active
        // provider kind + model for display when configured.
        g.MapGet("/status", (IAiProviderStore store, IOptionsMonitor<AppOptions> options) =>
        {
            var active = store.Active;
            var branding = options.CurrentValue.Branding;
            return Results.Ok(new
            {
                enabled = active is not null,
                kind = active?.Kind.ToString(),
                model = active?.Model,
                allowModelManagement = branding.AllowAiModelManagement
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

        // Built-in ONNX local model install state + a manual trigger, so the owner can see whether each
        // curated model is present/downloading/failed and kick the one-time background download from the UI.
        // The top-level installed/state stay for the shipped default (back-compat); models[] lists the whole
        // curated catalog so the user can install and switch between several local models.
        g.MapGet("/built-in/status", (IBuiltInModelInstaller installer) =>
            Results.Ok(new
            {
                installed = installer.IsInstalled(),
                state = installer.State.ToString(),
                models = installer.Catalog().Select(m => new
                {
                    key = m.Spec.Key,
                    name = m.Spec.DisplayName,
                    isDefault = m.Spec.IsDefault,
                    installed = m.Installed,
                    state = m.State.ToString()
                })
            }))
            .RequireAuthorization(AuthPolicies.Owner);

        g.MapPost("/built-in/install", (string? key, IBuiltInModelInstaller installer) =>
        {
            var target = string.IsNullOrWhiteSpace(key) ? Core.Ai.BuiltInModelCatalog.Default.Key : key!;
            installer.EnsureInstalling(target);
            return Results.Ok(new { installed = installer.IsInstalled(target), state = installer.StateOf(target).ToString() });
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
        }).AddEndpointFilter(BrandingGate(b => b.AllowAiModelManagement));

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

        // The models a user may target for a task/binding: their own providers plus the deployment defaults
        // (id + model + kind only — never a key). Powers the task create-dialog's model multi-select.
        g.MapGet("/usable-models", async (IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var mine = await store.ListForUserAsync(uid, ct);
            var deployment = await store.ListAsync(ct);
            var models = deployment.Concat(mine)
                .Select(p => new { id = p.Id, model = p.Model, kind = p.Kind.ToString(), isActive = p.IsActive })
                .ToList();
            return Results.Ok(models);
        });

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

        // Per-feature model bindings — model management, white-label/owner-gated by Branding.AllowAiModelManagement.
        // Deployment scope (owner-managed): bind each AI feature to a provider.
        var ownerBindings = g.MapGroup("/feature-bindings")
            .RequireAuthorization(AuthPolicies.Owner)
            .AddEndpointFilter(BrandingGate(b => b.AllowAiModelManagement));

        ownerBindings.MapGet("", async (IAiProviderStore store, CancellationToken ct) =>
            Results.Ok(await store.ListBindingsAsync(null, ct)));

        ownerBindings.MapPut("", async (SetFeatureBindingRequest req, IAiProviderStore store, CancellationToken ct) =>
        {
            try
            {
                await store.SetBindingAsync(null, req.Feature, Core.AiProviderCredentialId.From(req.CredentialId), ct);
                return Results.Ok(new { ok = true });
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        ownerBindings.MapDelete("/{feature}", async (AiFeature feature, IAiProviderStore store, CancellationToken ct) =>
        {
            await store.ClearBindingAsync(null, feature, ct);
            return Results.Ok(new { ok = true });
        });

        // Per-user feature bindings: a user's own binding overrides the deployment default for that feature.
        var userBindings = g.MapGroup("/my-feature-bindings")
            .AddEndpointFilter(BrandingGate(b => b.AllowAiModelManagement));

        userBindings.MapGet("", async (IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
            u.UserId is { } uid ? Results.Ok(await store.ListBindingsAsync(uid, ct)) : Results.Unauthorized());

        userBindings.MapPut("", async (SetFeatureBindingRequest req, IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            try
            {
                await store.SetBindingAsync(uid, req.Feature, Core.AiProviderCredentialId.From(req.CredentialId), ct);
                return Results.Ok(new { ok = true });
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        });

        userBindings.MapDelete("/{feature}", async (AiFeature feature, IAiProviderStore store, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            await store.ClearBindingAsync(uid, feature, ct);
            return Results.Ok(new { ok = true });
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
                .Select(i => new AiInstanceContext(i.CBot.Name, i.KindName, i.StatusName, i.Symbol, i.Timeframe, DigestDetailOf(i)))
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
            // Head-clip each report: the summary metrics (net profit, trades, drawdown, ratios) sit at the top
            // of the backtest report JSON, so a bounded head keeps the decay signal while two full reports
            // would overflow a small local model's context window (the same overflow that broke the digest).
            var previous = reports.Count > 1 ? ClipReport(reports[1]) : null;
            return Results.Ok(await ai.AssessStrategyDecayAsync(name, previous, ClipReport(reports[0]), current, AiConstants.TuneAdviceMaxTokens, ct));
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

        // AI Build project chat: the persisted conversation (prompts + model replies with timestamps) for a
        // cBot source project. The user iterates on one project over time; source edits from a prompt update
        // the project (via the builder), and the project is built/run through the builder endpoints.
        // The AI Build project list: ONLY the user's projects that have an AI Build conversation (i.e. a
        // session was started — a new cBot from scratch or an existing one picked to improve), not their
        // whole cBot library. A project joins this list the moment its session starts (a seed turn exists).
        g.MapGet("/build/projects", async (DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var withConversation = db.CBotBuildMessages.Where(m => m.UserId == uid).Select(m => m.ProjectId).Distinct();
            var projects = await db.CBotSourceProjects.AsNoTracking()
                .Where(p => p.UserId == uid && withConversation.Contains(p.Id))
                .Select(p => new
                {
                    p.Id, p.Name, Language = p.LanguageName, p.LastBuildAt, p.LastBuildSucceeded, p.CreatedAt, p.UpdatedAt
                })
                .ToListAsync(ct);
            return Results.Ok(projects);
        });

        // Start an AI Build session on a project (new-from-scratch or improve-existing). Idempotently seeds
        // the conversation with an opening turn so the project shows up in the AI Build list right away — even
        // before the user's first prompt — without listing every cBot the user owns.
        g.MapPost("/build/{projectId:guid}/start", async (
            Guid projectId, DataContext db, ICurrentUser u, TimeProvider clock, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var pid = CBotSourceProjectId.From(projectId);
            if (!await db.CBotSourceProjects.AnyAsync(p => p.Id == pid && p.UserId == uid, ct)) return Results.NotFound();
            if (!await db.CBotBuildMessages.AnyAsync(m => m.ProjectId == pid && m.UserId == uid, ct))
            {
                db.CBotBuildMessages.Add(Core.Domain.CBotBuildMessage.Create(
                    pid, uid, Core.Domain.CBotBuildRole.Assistant,
                    "Describe the cBot you want, or a change to make, and I'll write and update the source. "
                    + "Build and Run it from the buttons above.", clock.GetUtcNow()));
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(new { ok = true });
        });

        g.MapGet("/build/{projectId:guid}/messages", async (
            Guid projectId, DataContext db, ICurrentUser u, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var pid = CBotSourceProjectId.From(projectId);
            if (!await db.CBotSourceProjects.AnyAsync(p => p.Id == pid && p.UserId == uid, ct)) return Results.NotFound();
            var messages = await db.CBotBuildMessages.AsNoTracking()
                .Where(m => m.ProjectId == pid && m.UserId == uid)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { role = m.Role.ToString(), content = m.Content, createdAt = m.CreatedAt })
                .ToListAsync(ct);
            return Results.Ok(messages);
        });

        // Which of the caller's projects are generating a reply right now (in-memory, node-local). The list
        // page intersects this with its own projects to show a "Working" status per row.
        g.MapGet("/build/activity", (IAiBuildActivity activity) =>
            Results.Ok(new { working = activity.WorkingProjectIds() }));

        g.MapGet("/build/{projectId:guid}/status", async (
            Guid projectId, DataContext db, ICurrentUser u, IAiBuildActivity activity, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var pid = CBotSourceProjectId.From(projectId);
            if (!await db.CBotSourceProjects.AnyAsync(p => p.Id == pid && p.UserId == uid, ct)) return Results.NotFound();
            return Results.Ok(new { working = activity.IsWorking(pid) });
        });

        g.MapPost("/build/{projectId:guid}/prompt", async (
            Guid projectId, BuildPromptRequest req, DataContext db, ICurrentUser u, IAiClient aiClient,
            IAiCallContext callContext, IServiceScopeFactory scopeFactory, IAiBuildActivity activity,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            if (!aiClient.Enabled) return Results.Ok(new { accepted = false, error = AiConstants.DisabledMessage });
            if (string.IsNullOrWhiteSpace(req.Prompt)) return Results.BadRequest("enter a prompt");

            var pid = CBotSourceProjectId.From(projectId);
            if (!await db.CBotSourceProjects.AnyAsync(p => p.Id == pid && p.UserId == uid, ct)) return Results.NotFound();

            // Persist the user's turn, then run the generation DETACHED from this request so it keeps going if
            // the user navigates away (the request's CancellationToken no longer aborts it). The chosen model
            // (the scoped ?modelId= override) is captured and passed explicitly — the background scope has no
            // request-scoped IAiCallContext. Mark the project working so the UI shows a live status.
            db.CBotBuildMessages.Add(Core.Domain.CBotBuildMessage.Create(
                pid, uid, Core.Domain.CBotBuildRole.User, req.Prompt!, clock.GetUtcNow()));
            await db.SaveChangesAsync(ct);

            var credentialId = callContext.OverrideCredentialId;
            activity.MarkWorking(pid);
            _ = Task.Run(() => RunPromptAsync(scopeFactory, activity, pid, uid, req.Prompt!, credentialId));

            return Results.Accepted($"/api/ai/build/{projectId}/messages", new { accepted = true });
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

            var proposals = Web.Ai.ParamSuiteParser.Parse(suite.Text, count);
            if (proposals.Count == 0)
                return Results.Ok(new
                {
                    success = false,
                    error = "The AI did not return usable parameter sets in the required JSON format. Small local " +
                            "models often can't; pick a larger/cloud model from the Model selector above and retry.",
                    raw = ClipLog(suite.Text)
                });

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

    // Per-request model override: a feature page's model selector appends ?modelId=<credential>. The scoped
    // IAiCallContext is read by RoutingAiClient to force that provider for this one call, so any AI feature
    // runs on the model the user picked without threading it through every service method. An id the user may
    // not use resolves to null downstream and harmlessly falls back to the feature binding / default provider.
    public static TBuilder WithAiModelOverride<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            if (Guid.TryParse(ctx.HttpContext.Request.Query["modelId"], out var modelId) && modelId != Guid.Empty)
                ctx.HttpContext.RequestServices.GetRequiredService<IAiCallContext>()
                    .OverrideCredentialId = Core.AiProviderCredentialId.From(modelId);
            return await next(ctx);
        });
        return builder;
    }

    private const int MaxLogChars = 4000;

    // White-label gate for a branding bool (overlaid by the owner's runtime override on IOptionsMonitor).
    // A disabled surface returns 404 so it looks entirely absent, matching RequireFeature's behaviour.
    private static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> BrandingGate(
        Func<BrandingOptions, bool> allowed) =>
        async (ctx, next) =>
        {
            var options = ctx.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<AppOptions>>();
            return allowed(options.CurrentValue.Branding) ? await next(ctx) : Results.NotFound();
        };

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

    // Runs one AI Build turn detached from the HTTP request: generate/refine the source with the chosen
    // model, apply it to the project, and persist the model's reply — always clearing the working flag.
    private static async Task RunPromptAsync(
        IServiceScopeFactory scopeFactory, IAiBuildActivity activity,
        CBotSourceProjectId pid, UserId uid, string prompt, Core.AiProviderCredentialId? credentialId)
    {
        using var scope = scopeFactory.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<DataContext>();
        var ai = services.GetRequiredService<IAiFeatureService>();
        var protector = services.GetRequiredService<ISecretProtector>();
        var clock = services.GetRequiredService<TimeProvider>();
        try
        {
            var project = await db.CBotSourceProjects.FirstOrDefaultAsync(p => p.Id == pid && p.UserId == uid);
            if (project is null) return;

            var files = DecryptFiles(project, protector);
            var codeKey = files.Keys.FirstOrDefault(k => k.EndsWith(project.FileExtension, StringComparison.Ordinal));
            var currentCode = codeKey is not null ? files[codeKey] : string.Empty;
            var description = string.IsNullOrWhiteSpace(currentCode)
                ? prompt
                : $"Current cBot source:\n{Truncate(currentCode, 8000)}\n\nApply this change and return the COMPLETE updated source:\n{prompt}";

            var gen = await ai.GenerateCBotAsync(project.LanguageName, description, credentialId, CancellationToken.None);
            if (!gen.Success)
            {
                db.CBotBuildMessages.Add(Core.Domain.CBotBuildMessage.Create(
                    pid, uid, Core.Domain.CBotBuildRole.Assistant, gen.Error ?? "AI request failed", clock.GetUtcNow()));
                await db.SaveChangesAsync();
                return;
            }

            if (codeKey is not null)
            {
                files[codeKey] = ExtractCode(gen.Text);
                project.SetFiles(protector.Protect(
                    Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files)), EncryptionPurposes.CbotSource));
                await db.SaveChangesAsync();
            }

            db.CBotBuildMessages.Add(Core.Domain.CBotBuildMessage.Create(
                pid, uid, Core.Domain.CBotBuildRole.Assistant, gen.Text, clock.GetUtcNow()));
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            services.GetRequiredService<ILogger<AiBuildActivity>>().AiBuildPromptFailed(pid.Value, ex);
            try
            {
                db.CBotBuildMessages.Add(Core.Domain.CBotBuildMessage.Create(
                    pid, uid, Core.Domain.CBotBuildRole.Assistant, "Generation failed — please try again.", clock.GetUtcNow()));
                await db.SaveChangesAsync();
            }
            catch { /* best-effort error turn */ }
        }
        finally
        {
            activity.Clear(pid);
        }
    }

    private static Dictionary<string, string> DecryptFiles(CBotSourceProject project, ISecretProtector protector)
    {
        if (project.EncryptedProjectFiles.Length == 0) return new Dictionary<string, string>();
        var json = Encoding.UTF8.GetString(protector.Unprotect(project.EncryptedProjectFiles, EncryptionPurposes.CbotSource));
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
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

    private static string? DetailOf(Instance instance) => instance switch
    {
        FailedRunInstance f => $"Failure: {f.FailureReason}",
        FailedBacktestInstance f => $"Failure: {f.FailureReason}",
        CompletedBacktestInstance c => c.ReportJson is null ? null : $"Report JSON:\n{c.ReportJson}",
        _ => null
    };

    // A concise per-bot detail for the portfolio digest. Deliberately omits the full backtest report JSON:
    // the digest reasons across up to DigestMaxInstances bots at once, and stuffing each one's whole report
    // in overflows a small local model's context window (the built-in ONNX model's 4096-token window).
    private static string? DigestDetailOf(Instance instance) => instance switch
    {
        FailedRunInstance f => $"Failure: {Truncate(f.FailureReason, 160)}",
        FailedBacktestInstance f => $"Failure: {Truncate(f.FailureReason, 160)}",
        _ => null
    };

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    // A backtest report's summary metrics lead the JSON; a bounded head keeps them small enough that two
    // reports (tune advisor) fit a small local model's context window.
    private const int MaxReportChars = 3500;
    private static string ClipReport(string reportJson) => Truncate(reportJson, MaxReportChars);
}

public sealed record UpsertProviderRequest(
    Guid? Id, Core.Ai.AiProviderKind Kind, string? BaseUrl, string? Model, string? ApiKey,
    int? MaxTokens, CapabilitiesRequest? Capabilities, bool Activate);
public sealed record CapabilitiesRequest(
    bool SupportsWebSearch, bool SupportsVision, bool SupportsSystemRole, bool SupportsTools);
public sealed record ProbeModelsRequest(Core.Ai.AiProviderKind Kind, string? BaseUrl, string? ApiKey);
public sealed record SetFeatureBindingRequest(Core.Ai.AiFeature Feature, Guid CredentialId);
public sealed record GenerateCBotRequest(string? Language, string? Description);
public sealed record ReviewCBotRequest(string? Language, string? Source);
public sealed record DebateRequest(string? Name, string? Language, string? Source);
public sealed record SentimentRequest(string? Symbol);
public sealed record VisionRequest(string? MediaType, string? Base64, string? Note);
public sealed record CurateRequest(string? Name, string? Language, string? Source);
public sealed record RecommendCopyProfileRequest(string? RiskProfile, string? SourceDescription);
public sealed record GenerateProjectRequest(string? Name, string? Language, string? Description);
public sealed record BuildPromptRequest(string? Prompt);
public sealed record OptimizeRunRequest(
    Guid TradingAccountId, string? Symbol, string? Timeframe,
    string? DockerImageTag, string? BacktestSettingsJson, int? Count);
