using System.Text;
using System.Text.Json;
using Core;
using Core.Ai;
using Core.Constants;
using Infrastructure.Builder;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Ai;

public sealed record CBotBuildFlowResult(
    bool Success, Guid? CBotId, Guid? ProjectId, int Attempts, string Log, string? Code, string Language, string? Error,
    string ProjectName);

/// <summary>
/// The plain-English-intent → generate → build → self-repair → create-cBot pipeline, behind the
/// synchronous <c>/api/ai/build-strategy</c> endpoint. Generates the cBot source with the caller's chosen
/// model, builds it in the sandboxed container, feeds compile errors back to the AI up to three times, and
/// on success creates a runnable <see cref="CBot"/>. Emits progress lines via <paramref name="onLog"/> for
/// the caller to surface; never throws for an AI/build failure — it returns a failed result (only a genuine
/// infrastructure fault propagates).
/// </summary>
public sealed class CBotBuildFlow(
    DataContext db, IAiFeatureService ai, ISecretProtector protector, CBotBuilder builder)
{
    private const int MaxAttempts = 3;
    private const int MaxLogChars = 4000;

    public async Task<CBotBuildFlowResult> BuildAsync(
        UserId uid, string name, string language, string description,
        AiProviderCredentialId? credentialId, Action<string>? onLog, CancellationToken ct)
    {
        void Log(string message) => onLog?.Invoke(message);

        var lang = string.IsNullOrWhiteSpace(language) ? "CSharp" : language;
        var desired = string.IsNullOrWhiteSpace(name) ? "AiBot" : name.Trim();
        var projectName = await UniqueNameAsync(
            db.CBotSourceProjects.Where(p => p.UserId == uid).Select(p => p.Name), desired, ct);
        CBotSourceProject project = lang.Equals("Python", StringComparison.OrdinalIgnoreCase)
            ? PythonProject.Create(uid, projectName)
            : CSharpProject.Create(uid, projectName);

        var files = JsonSerializer.Deserialize<Dictionary<string, string>>(
            Templates.CreateProjectJson(project.LanguageName, project.Name)) ?? new Dictionary<string, string>();
        var codeKey = files.Keys.FirstOrDefault(k => k.EndsWith(project.FileExtension, StringComparison.Ordinal));
        if (codeKey is null)
            return new CBotBuildFlowResult(false, null, project.Id.Value, 0, string.Empty, null, lang, "project template has no code file", projectName);

        Log("Generating strategy code…");
        var gen = await ai.GenerateCBotAsync(lang, description, credentialId, ct);
        if (!gen.Success)
            return new CBotBuildFlowResult(false, null, project.Id.Value, 0, string.Empty, null, lang, gen.Error, projectName);
        files[codeKey] = ExtractCode(gen.Text);

        db.CBotSourceProjects.Add(project);

        var success = false;
        var log = string.Empty;
        byte[]? algo = null;
        var attempts = 0;
        for (attempts = 1; attempts <= MaxAttempts; attempts++)
        {
            Log($"Build attempt {attempts} of {MaxAttempts}…");
            project.SetFiles(protector.Protect(
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files)), EncryptionPurposes.CbotSource));
            await db.SaveChangesAsync(ct);

            var build = await builder.BuildAsync(project, uid, ct);
            success = build.Success;
            log = build.Log;
            algo = build.AlgoBytes;
            if (success || attempts == MaxAttempts) break;

            Log("Build failed — asking the AI to fix the compile errors…");
            var fix = await ai.FixCBotAsync(lang, files[codeKey], build.Log, credentialId, ct);
            if (!fix.Success)
            {
                Log("AI fix step failed.");
                break;
            }
            files[codeKey] = ExtractCode(fix.Text);
        }

        if (!success || algo is null)
        {
            // The source project is already persisted (saved each build attempt), so a failed AI build still
            // shows up in the user's cBots list as a "Failed" project they can open in the editor and fix.
            Log($"Build did not succeed after {attempts} attempt(s). Saved as '{project.Name}' in your cBots — open it in the editor to fix and rebuild.");
            return new CBotBuildFlowResult(false, null, project.Id.Value, attempts, ClipLog(log), files[codeKey], lang, null, projectName);
        }

        try
        {
            var cbotName = await UniqueNameAsync(db.CBots.Where(c => c.UserId == uid).Select(c => c.Name), project.Name, ct);
            var cbot = CBot.Create(uid, cbotName, protector.Protect(algo, EncryptionPurposes.CbotAlgo), project.Id);
            db.CBots.Add(cbot);
            await db.SaveChangesAsync(ct);
            Log($"Build succeeded — your runnable cBot '{project.Name}' is ready.");
            return new CBotBuildFlowResult(true, cbot.Id.Value, project.Id.Value, attempts, ClipLog(log), files[codeKey], lang, null, projectName);
        }
        catch (DbUpdateException)
        {
            return new CBotBuildFlowResult(false, null, project.Id.Value, attempts, ClipLog(log), files[codeKey], lang,
                "a cBot or project with that name already exists — try another name", projectName);
        }
    }

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
}
