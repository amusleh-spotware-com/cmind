using System.Text;
using Core;
using Core.Constants;
using Core.NodeAgent;
using CtraderCliNode;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

var serviceVersion = VersionInfo.Product;
var environmentName = builder.Environment.EnvironmentName;
builder.Services.AddSerilog((sp, lc) =>
{
    lc.MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With<TraceActivityEnricher>()
        .Enrich.WithProperty(ObservabilityDefaults.ServiceNameProperty, ObservabilityDefaults.NodeAgentServiceName)
        .Enrich.WithProperty(ObservabilityDefaults.ServiceVersionProperty, serviceVersion)
        .Enrich.WithProperty(ObservabilityDefaults.ServiceNamespaceProperty, ObservabilityDefaults.ServiceNamespace)
        .Enrich.WithProperty(ObservabilityDefaults.DeploymentEnvironmentProperty, environmentName)
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(sp)
        .WriteTo.Console(new RenderedCompactJsonFormatter());

    var otlpEndpoint = builder.Configuration[ObservabilityDefaults.OtlpEndpointKey];
    if (!string.IsNullOrWhiteSpace(otlpEndpoint))
    {
        lc.WriteTo.OpenTelemetry(o =>
        {
            o.Endpoint = otlpEndpoint;
            o.ResourceAttributes = new Dictionary<string, object>
            {
                [ObservabilityDefaults.ServiceNameProperty] = ObservabilityDefaults.NodeAgentServiceName,
                [ObservabilityDefaults.ServiceVersionProperty] = serviceVersion,
                [ObservabilityDefaults.ServiceNamespaceProperty] = ObservabilityDefaults.ServiceNamespace,
                [ObservabilityDefaults.DeploymentEnvironmentProperty] = environmentName
            };
        });
    }
});

builder.Services.AddOptions<NodeAgentOptions>()
    .Bind(builder.Configuration.GetSection(NodeAgentOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddHttpClient(NodeRegistrationClient.HttpClientName);
builder.Services.AddHostedService<NodeRegistrationClient>();

var secret = builder.Configuration.GetSection(NodeAgentOptions.SectionName)[nameof(NodeAgentOptions.JwtSecret)] ?? string.Empty;
if (secret.Length < NodeAgentAuth.MinSecretLength)
    throw new InvalidOperationException(
        $"{NodeAgentOptions.SectionName}:{nameof(NodeAgentOptions.JwtSecret)} must be at least {NodeAgentAuth.MinSecretLength} characters.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = NodeAgentAuth.Issuer,
            ValidateAudience = true,
            ValidAudience = NodeAgentAuth.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            // Pin the signing algorithm so a token is accepted only when signed with HS256 — defence in
            // depth against algorithm-confusion, matching the calendar JWT validator.
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet(HealthEndpoints.Health, () => Results.Ok("Healthy")).AllowAnonymous();

var api = app.MapGroup(NodeAgentRoutes.Base).RequireAuthorization();

api.AddEndpointFilter(async (ctx, next) =>
{
    var header = ctx.HttpContext.Request.Headers[NodeAgentProtocol.HeaderName].ToString();
    if (!int.TryParse(header, out var clientVersion) || clientVersion != NodeAgentProtocol.Version)
        return Results.Problem(
            $"Protocol version mismatch: agent speaks {NodeAgentProtocol.Version}, caller sent '{header}'.",
            statusCode: StatusCodes.Status426UpgradeRequired);
    return await next(ctx);
});

api.MapGet("/info", () =>
    Results.Ok(new NodeAgentInfoResponse(VersionInfo.Product, NodeAgentProtocol.Version)));

api.MapPost("/containers", async (StartContainerRequest req, DockerService docker, CancellationToken ct) =>
{
    try
    {
        var res = await docker.StartAsync(req, ct);
        return Results.Ok(res);
    }
    catch (DockerService.ImageNotAllowedException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
    catch (InvalidOperationException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway); }
});

api.MapGet("/containers/{containerId}/status", async (string containerId, DockerService docker, CancellationToken ct) =>
    Results.Ok(await docker.GetStatusAsync(containerId, ct)));

api.MapGet("/containers/{containerId}/report", async (string containerId, DockerService docker, CancellationToken ct) =>
{
    var report = await docker.ReadReportAsync(containerId, ct);
    return report is null ? Results.NotFound() : Results.Text(report, "application/json");
});

api.MapGet("/containers/{containerId}/report-html", async (string containerId, DockerService docker, CancellationToken ct) =>
{
    var report = await docker.ReadReportHtmlAsync(containerId, ct);
    return report is null ? Results.NotFound() : Results.Text(report, "text/html");
});

api.MapPost("/containers/{containerId}/stop", async (string containerId, DockerService docker, CancellationToken ct) =>
{
    await docker.StopAsync(containerId, ct);
    return Results.Ok();
});

api.MapGet("/containers/{containerId}/logs", (string containerId, DockerService docker, HttpContext ctx) =>
    StreamLogsAsync(containerId, docker, ctx));

api.MapGet("/node/stats", async (DockerService docker, CancellationToken ct) =>
    Results.Ok(await docker.CollectStatsAsync(ct)));

api.MapPost("/node/clean", async (Guid? userId, DockerService docker, CancellationToken ct) =>
{
    await docker.CleanAsync(userId, ct);
    return Results.Ok();
});

app.Run();

static async Task StreamLogsAsync(string containerId, DockerService docker, HttpContext ctx)
{
    ctx.Response.ContentType = "text/plain";
    await foreach (var line in docker.TailLogsAsync(containerId, ctx.RequestAborted))
    {
        await ctx.Response.WriteAsync(line + "\n", ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
    }
}

public partial class Program;
