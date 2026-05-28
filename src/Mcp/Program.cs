using Core.Constants;
using Infrastructure;
using Infrastructure.Persistence;
using Mcp.Auth;
using Mcp.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.AddNpgsqlDbContext<CtwDbContext>(ConnectionStrings.CtwDb);
builder.Services.AddCtwInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(AuthSchemes.McpKey)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, McpKeyAuthHandler>(
        AuthSchemes.McpKey, _ => { });
builder.Services.AddAuthorization();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<CBotTools>()
    .WithTools<InstanceTools>();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();

app.Run();

public partial class Program;
