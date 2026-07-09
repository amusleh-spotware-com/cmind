using Core.Constants;
using Core.Options;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Nodes.CopyTrading;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString(ConnectionStrings.AppDb)
    ?? throw new InvalidOperationException("Missing connection string 'appdb'.");

builder.Services.AddDbContext<DataContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddInfrastructure(builder.Configuration);

// Standing per-node copy host. Runs the same in-process supervisor against the shared database;
// enable App:Copy:Enabled here (and disable it on the web host) to move copy hosting onto this node.
builder.Services.AddHostedService<CopyEngineSupervisor>();
builder.Services.AddHostedService<OpenApiTokenRefreshService>();

var host = builder.Build();
host.Run();
