using System.Text;
using System.Text.Json;
using Core;
using Core.Constants;
using Infrastructure.Persistence;
using Infrastructure.Builder;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateProjectRequest(string Name, int Language);
public record BuildRequest(string? Code, string? ProjectFile);
public record ProjectFilesRequest(Dictionary<string, string> Files);

public static class BuilderEndpoints
{
    public static IEndpointRouteBuilder MapBuilderEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/builder").RequireAuthorization("UserOrAbove");

        g.MapGet("/projects", async (DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.CBotSourceProjects.Where(p => p.UserId == uid)
                .Select(p => new { p.Id, p.Name, Language = p.LanguageName, p.LastBuildAt, p.LastBuildSucceeded })
                .ToListAsync();
        });

        g.MapPost("/projects", async (CreateProjectRequest req, DataContext db, ICurrentUser u, ISecretProtector protector) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            CBotSourceProject project = req.Language == 1
                ? PythonProject.Create(uid, req.Name)
                : CSharpProject.Create(uid, req.Name);
            var json = Templates.CreateProjectJson(project.LanguageName, req.Name);
            project.SetFiles(protector.Protect(Encoding.UTF8.GetBytes(json), EncryptionPurposes.CbotSource));
            db.CBotSourceProjects.Add(project);
            await db.SaveChangesAsync();
            return Results.Ok(new { project.Id });
        });

        g.MapGet("/projects/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            return Results.Ok(new
            {
                p.Id,
                p.Name,
                Language = p.LanguageName,
                p.LastBuildAt,
                p.LastBuildSucceeded,
                p.LastBuildLog
            });
        });

        g.MapDelete("/projects/{id:guid}", async (Guid id, DataContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            db.CBotSourceProjects.Remove(p);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapGet("/projects/{id:guid}/files", async (Guid id, DataContext db, ICurrentUser u, ISecretProtector protector) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            var json = p.EncryptedProjectFiles.Length == 0
                ? "{}"
                : Encoding.UTF8.GetString(protector.Unprotect(p.EncryptedProjectFiles, EncryptionPurposes.CbotSource));
            var files = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                        ?? new Dictionary<string, string>();
            return Results.Ok(new { files });
        });

        g.MapPut("/projects/{id:guid}/files", async (Guid id, ProjectFilesRequest req,
            DataContext db, ICurrentUser u, ISecretProtector protector) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            var json = JsonSerializer.Serialize(req.Files);
            p.SetFiles(protector.Protect(Encoding.UTF8.GetBytes(json), EncryptionPurposes.CbotSource));
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        g.MapPost("/projects/{id:guid}/build", async (Guid id, BuildRequest? req,
            DataContext db, ICurrentUser u, ISecretProtector protector, CBotBuilder builder) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();

            // Back-compat: if a Code/ProjectFile body is supplied, merge it into stored files.
            if (req is not null && (req.Code is not null || req.ProjectFile is not null))
            {
                var existingJson = p.EncryptedProjectFiles.Length == 0
                    ? "{}"
                    : Encoding.UTF8.GetString(protector.Unprotect(p.EncryptedProjectFiles, EncryptionPurposes.CbotSource));
                var files = JsonSerializer.Deserialize<Dictionary<string, string>>(existingJson)
                            ?? new Dictionary<string, string>();
                if (req.Code is not null)
                {
                    var codeFile = files.Keys.FirstOrDefault(k =>
                        k.EndsWith(".cs", StringComparison.Ordinal) || k.EndsWith(".py", StringComparison.Ordinal));
                    if (codeFile is not null) files[codeFile] = req.Code;
                }
                if (req.ProjectFile is not null)
                {
                    var projFile = files.Keys.FirstOrDefault(k =>
                        k.EndsWith(".csproj", StringComparison.Ordinal) || k == "pyproject.toml");
                    if (projFile is not null) files[projFile] = req.ProjectFile;
                }
                var newJson = JsonSerializer.Serialize(files);
                p.SetFiles(protector.Protect(Encoding.UTF8.GetBytes(newJson), EncryptionPurposes.CbotSource));
                await db.SaveChangesAsync();
            }

            var result = await builder.BuildAsync(p, uid, default);
            return Results.Ok(new { success = result.Success, log = result.Log });
        });

        g.MapPost("/projects/{id:guid}/quick-run", async (Guid id, DataContext db, ICurrentUser u,
            CBotBuilder builder, IContainerDispatcherFactory factory, ISecretProtector protector,
            INodeScheduler scheduler, TimeProvider timeProvider) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            var br = await builder.BuildAsync(p, uid, default);
            if (!br.Success || br.AlgoBytes is null)
                return Results.Ok(new { success = false, output = br.Log, instanceId = (Guid?)null });

            var node = await scheduler.PickNodeAsync("Run", default);
            if (node is null) return Results.Conflict("no node available");

            var cbot = await db.CBots.FirstAsync(c => c.SourceProjectId == p.Id);
            var starting = RunInstance.CreateStarting(uid, cbot.Id, node.Id, DockerImageTag.Latest,
                new Symbol("EURUSD"), new Timeframe("h1"));
            db.Instances.Add(starting);
            await db.SaveChangesAsync();
            starting.AttachNode(node);

            string containerId;
            try
            {
                containerId = await factory.For(node).StartAsync(starting, br.AlgoBytes, "{}", default);
            }
            catch (Exception ex)
            {
                var failed = starting.ToFailed(ex.Message, timeProvider.GetUtcNow());
                db.Instances.Remove(starting);
                db.Instances.Add(failed);
                await db.SaveChangesAsync();
                return Results.Ok(new { success = false, output = ex.Message, instanceId = (Guid?)null });
            }

            var running = starting.ToRunning(containerId, timeProvider.GetUtcNow());
            db.Instances.Remove(starting);
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { success = true, output = br.Log, instanceId = (Guid?)running.Id.Value });
        });

        return app;
    }
}
