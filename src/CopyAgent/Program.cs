using Core.Constants;
using Core.Options;
using Infrastructure;
using Infrastructure.Observability;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nodes.CopyTrading;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddStructuredLogging(
    builder.Configuration, ObservabilityDefaults.CopyAgentServiceName, builder.Environment.EnvironmentName);

builder.Services
    .AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString(ConnectionStrings.AppDb)
    ?? throw new InvalidOperationException("Missing connection string 'appdb'.");

builder.Services.AddDbContext<DataContext>(options => options.UseAppNpgsql(connectionString));
builder.Services.AddInfrastructure(builder.Configuration);

// Copy engine dependencies the supervisor needs (the web host gets these from AddNodes; this standalone
// agent wires them directly). No transparency/notification drainers here — this node just executes copies —
// so both sinks are the no-op default; the live-log broker is in-process for symmetry.
builder.Services.AddSingleton<CopyLogBroker>();
builder.Services.AddSingleton<Core.CopyTrading.ICopyLogSink>(sp => sp.GetRequiredService<CopyLogBroker>());
builder.Services.AddSingleton<Core.CopyTrading.ICopyLogFeed>(sp => sp.GetRequiredService<CopyLogBroker>());
builder.Services.AddSingleton<Core.CopyTrading.ICopyEventSink>(Core.CopyTrading.NullCopyEventSink.Instance);
builder.Services.AddSingleton<Core.CopyTrading.ICopyNotificationSink>(Core.CopyTrading.NullCopyNotificationSink.Instance);

// Standing per-node copy host. Runs the same in-process supervisor against the shared database; gated by
// the single App:Features:CopyTrading flag (like the web host). Copy profiles are claimed by exactly one
// node via the DB lease, so running this agent alongside the web host never double-executes.
builder.Services.AddHostedService<CopyEngineSupervisor>();
builder.Services.AddHostedService<OpenApiTokenRefreshService>();

var host = builder.Build();
host.Run();
