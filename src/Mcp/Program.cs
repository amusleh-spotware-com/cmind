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

builder.Services.AddStructuredLogging(builder.Configuration, ObservabilityDefaults.McpServiceName);
builder.AddNpgsqlDbContext<DataContext>(ConnectionStrings.AppDb);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(AuthSchemes.McpKey)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, McpKeyAuthHandler>(
        AuthSchemes.McpKey, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddMcpServer()
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<CBotTools>()
    .WithTools<InstanceTools>()
    .WithTools<AiTools>()
    .WithTools<CopyTools>();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.MapGet(HealthEndpoints.Version, () => Results.Ok(new NodeAgentInfoResponse(VersionInfo.Product, NodeAgentProtocol.Version)))
    .AllowAnonymous();

app.Run();

public partial class Program;
