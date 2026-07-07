using Core;
using Core.Constants;
using Core.NodeAgent;
using Infrastructure;
using Infrastructure.Persistence;
using Mcp.Auth;
using Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

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
    .WithTools<AiTools>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.MapGet(HealthEndpoints.Version, () => Results.Ok(new NodeAgentInfoResponse(VersionInfo.Product, NodeAgentProtocol.Version)))
    .AllowAnonymous();

app.Run();

public partial class Program;
