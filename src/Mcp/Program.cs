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
builder.AddNpgsqlDbContext<DataContext>(ConnectionStrings.AppDb, settings =>
{
    settings.DisableRetry = false;
    settings.CommandTimeout = DatabaseDefaults.CommandTimeoutSeconds;
});
// The MCP server is a read-only tool surface: it must NOT run the DB-polling background workers (agent
// runtime, calendar ingest/backfill/webhook) — the Web host + nodes own those. Running them here too made
// two processes race the same one-time backfill on a fresh DB (duplicate-key 23505).
builder.Services.AddInfrastructure(builder.Configuration, backgroundServices: false);
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
if (features.PropFirm) mcp.WithTools<PropFirmTools>();
if (features.EconomicCalendar) mcp.WithTools<CalendarTools>();

var app = builder.Build();

// Apply migrations under the shared advisory lock BEFORE serving, so a fresh database does not log a burst
// of "relation does not exist" errors when the settings readers and the DataProtection keyring first touch
// it. Idempotent and cross-process safe: if the Web host already migrated, this is a no-op.
await DatabaseMigrator.MigrateAsync(app.Services);

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();
app.MapMcp("/mcp").RequireAuthorization();
app.MapGet(HealthEndpoints.Version, () => Results.Ok(new NodeAgentInfoResponse(VersionInfo.Product, NodeAgentProtocol.Version)))
    .AllowAnonymous();

app.Run();

public partial class Program;
