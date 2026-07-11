using Core;
using Core.Constants;
using Core.NodeAgent;
using Infrastructure;
using Infrastructure.Observability;
using Infrastructure.Persistence;
using Mcp.Auth;
using Mcp.Tools;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStructuredLogging(
    builder.Configuration, ObservabilityDefaults.McpServiceName, builder.Environment.EnvironmentName);
builder.Services.AddAppTelemetry(
    builder.Configuration, ObservabilityDefaults.McpServiceName, builder.Environment.EnvironmentName);
builder.AddNpgsqlDbContext<DataContext>(ConnectionStrings.AppDb);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(AuthSchemes.McpKey)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, McpKeyAuthHandler>(
        AuthSchemes.McpKey, _ => { });
builder.Services.AddAuthorization();

var features = builder.Configuration.GetSection(Core.Options.AppOptions.SectionName)
    .Get<Core.Options.AppOptions>()?.Features ?? new Core.Options.FeaturesOptions();

var mcp = builder.Services.AddMcpServer().WithHttpTransport(o => o.Stateless = true);
if (features.Authoring) mcp.WithTools<CBotTools>();
if (features.Execution) mcp.WithTools<InstanceTools>();
if (features.Ai) mcp.WithTools<AiTools>();
if (features.CopyTrading) mcp.WithTools<CopyTools>();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.MapGet(HealthEndpoints.Version, () => Results.Ok(new NodeAgentInfoResponse(VersionInfo.Product, NodeAgentProtocol.Version)))
    .AllowAnonymous();

app.Run();

public partial class Program;
