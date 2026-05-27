using Infrastructure;
using Infrastructure.Persistence;
using Mcp.Auth;
using Mcp.Tools;
using Infrastructure.Aspire;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CtwDbContext>("ctwdb");
builder.Services.AddCtwInfrastructure(builder.Configuration);

builder.Services.AddAuthentication("McpKey")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, McpKeyAuthHandler>("McpKey", _ => { });
builder.Services.AddAuthorization();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<CBotTools>()
    .WithTools<InstanceTools>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

public partial class Program;
