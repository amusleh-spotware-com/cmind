using System.Text.Json;
using Core;
using Infrastructure.Persistence;
using Nodes.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Endpoints;

public record CreateProjectRequest(string Name, int Language);
public record BuildRequest(string? Code, string? ProjectFile);

public static class BuilderEndpoints
{
    public static IEndpointRouteBuilder MapBuilderEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/builder").RequireAuthorization("UserOrAbove");

        g.MapGet("/projects", async (CtwDbContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            return await db.CBotSourceProjects.Where(p => p.UserId == uid)
                .Select(p => new { p.Id, p.Name, Language = p.LanguageName, p.LastBuildAt, p.LastBuildSucceeded })
                .ToListAsync();
        });

        g.MapPost("/projects", async (CreateProjectRequest req, CtwDbContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            CBotSourceProject project = req.Language == 1
                ? new PythonProject()
                : new CSharpProject();
            project.UserId = uid;
            project.Name = req.Name;
            project.ProjectFilesJson = Templates.CreateProjectJson(project.LanguageName, req.Name);
            db.CBotSourceProjects.Add(project);
            await db.SaveChangesAsync();
            return Results.Ok(new { project.Id });
        });

        g.MapGet("/projects/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });

        g.MapPost("/projects/{id:guid}/build", async (Guid id, BuildRequest req,
            CtwDbContext db, ICurrentUser u, CBotBuilder builder) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();

            var files = JsonSerializer.Deserialize<Dictionary<string, string>>(p.ProjectFilesJson)
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
            p.ProjectFilesJson = JsonSerializer.Serialize(files);
            await db.SaveChangesAsync();

            var result = await builder.BuildAsync(p, uid, default);
            return Results.Ok(new { success = result.Success, log = result.Log });
        });

        g.MapPost("/projects/{id:guid}/quick-run", async (Guid id, CtwDbContext db, ICurrentUser u,
            CBotBuilder builder, IContainerDispatcher dispatcher, ISecretProtector protector,
            INodeScheduler scheduler) =>
        {
            var uid = u.UserId!.Value;
            var pid = CBotSourceProjectId.From(id);
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == pid && x.UserId == uid);
            if (p is null) return Results.NotFound();
            var br = await builder.BuildAsync(p, uid, default);
            if (!br.Success || br.AlgoBytes is null) return Results.Ok(new { output = br.Log });

            var node = await scheduler.PickNodeAsync("Run", default);
            if (node is null) return Results.Conflict("no node available");

            var cbot = await db.CBots.FirstAsync(c => c.SourceProjectId == p.Id);
            var starting = new StartingRunInstance
            {
                UserId = uid,
                CBotId = cbot.Id,
                NodeId = node.Id,
                DockerImageTag = "latest",
                Symbol = "EURUSD",
                Timeframe = "h1"
            };
            db.Instances.Add(starting);
            await db.SaveChangesAsync();
            starting.Node = node;
            var containerId = await dispatcher.StartAsync(starting, br.AlgoBytes, "{}", default);

            db.Instances.Remove(starting);
            var running = new RunningRunInstance
            {
                UserId = uid,
                CBotId = cbot.Id,
                NodeId = node.Id,
                DockerImageTag = "latest",
                Symbol = "EURUSD",
                Timeframe = "h1",
                ContainerId = containerId,
                StartedAt = DateTimeOffset.UtcNow
            };
            db.Instances.Add(running);
            await db.SaveChangesAsync();
            return Results.Ok(new { output = br.Log, instanceId = running.Id });
        });

        return app;
    }
}
