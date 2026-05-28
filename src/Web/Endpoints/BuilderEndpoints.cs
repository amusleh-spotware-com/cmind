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
            var raw = await db.CBotSourceProjects.Where(p => p.UserId == u.UserId)
                .Select(p => new { p.Id, p.Name, p.Language, p.LastBuildAt, p.LastBuildSucceeded })
                .ToListAsync();
            return raw.Select(p => new { p.Id, p.Name, Language = p.Language.Name,
                p.LastBuildAt, p.LastBuildSucceeded }).ToList();
        });

        g.MapPost("/projects", async (CreateProjectRequest req, CtwDbContext db, ICurrentUser u) =>
        {
            if (u.UserId is not { } uid) return Results.Unauthorized();
            var lang = req.Language == 1 ? CBotLanguage.Python : CBotLanguage.CSharp;
            var project = new CBotSourceProject
            {
                UserId = uid,
                Name = req.Name,
                Language = lang,
                ProjectFilesJson = Templates.CreateProjectJson(lang, req.Name)
            };
            db.CBotSourceProjects.Add(project);
            await db.SaveChangesAsync();
            return Results.Ok(new { project.Id });
        });

        g.MapGet("/projects/{id:guid}", async (Guid id, CtwDbContext db, ICurrentUser u) =>
        {
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
            return p is null ? Results.NotFound() : Results.Ok(p);
        });

        g.MapPost("/projects/{id:guid}/build", async (Guid id, BuildRequest req,
            CtwDbContext db, ICurrentUser u, CBotBuilder builder) =>
        {
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
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

            var result = await builder.BuildAsync(p, u.UserId!.Value, default);
            return Results.Ok(new { success = result.Success, log = result.Log });
        });

        g.MapPost("/projects/{id:guid}/quick-run", async (Guid id, CtwDbContext db, ICurrentUser u,
            CBotBuilder builder, IContainerDispatcher dispatcher, ISecretProtector protector,
            INodeScheduler scheduler) =>
        {
            var p = await db.CBotSourceProjects.FirstOrDefaultAsync(x => x.Id == id && x.UserId == u.UserId);
            if (p is null) return Results.NotFound();
            var br = await builder.BuildAsync(p, u.UserId!.Value, default);
            if (!br.Success || br.AlgoBytes is null) return Results.Ok(new { output = br.Log });

            var node = await scheduler.PickNodeAsync(InstanceType.Run, default);
            if (node is null) return Results.Conflict("no node available");

            var instance = new Instance
            {
                UserId = u.UserId.Value,
                CBotId = (await db.CBots.FirstAsync(c => c.SourceProjectId == p.Id)).Id,
                NodeId = node.Id,
                Node = node,
                Type = InstanceType.Run,
                Status = InstanceStatus.Starting,
                DockerImageTag = "latest",
                Symbol = "EURUSD",
                Timeframe = "h1",
                StartedAt = DateTimeOffset.UtcNow
            };
            db.Instances.Add(instance);
            await db.SaveChangesAsync();
            instance.ContainerId = await dispatcher.StartAsync(instance, br.AlgoBytes, "{}", default);
            instance.Status = InstanceStatus.Running;
            await db.SaveChangesAsync();
            return Results.Ok(new { output = br.Log, instanceId = instance.Id });
        });

        return app;
    }
}
