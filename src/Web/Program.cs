using Core;
using Core.Constants;
using Core.Options;
using Infrastructure;
using Infrastructure.Aspire;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using Nodes;
using Web.Auth;
using Web.Components;
using Web.Endpoints;
using Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CtwDbContext>(ConnectionStrings.CtwDb);

builder.Services
    .AddOptions<CtwOptions>()
    .Bind(builder.Configuration.GetSection(CtwOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddCtwInfrastructure(builder.Configuration);
builder.Services.AddCtwNodes();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => builder.Configuration.GetConnectionString(ConnectionStrings.CtwDb)
                     ?? throw new InvalidOperationException("Missing connection string"),
        name: "postgres", tags: ["ready"]);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/forbidden";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.Owner, p => p.RequireRole(nameof(UserRole.Owner)))
    .AddPolicy(AuthPolicies.AdminOrAbove, p => p.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Admin)))
    .AddPolicy(AuthPolicies.UserOrAbove, p => p.RequireRole(
        nameof(UserRole.Owner), nameof(UserRole.Admin), nameof(UserRole.User)));

builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<OwnerSeeder>();
builder.Services.AddHostedService<InstanceReconciler>();

var app = builder.Build();
app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapAuthEndpoints();
app.MapCBotEndpoints();
app.MapNodeEndpoints();
app.MapInstanceEndpoints();
app.MapUserEndpoints();
app.MapCtidEndpoints();
app.MapParamSetEndpoints();
app.MapMcpKeyEndpoints();
app.MapBuilderEndpoints();
app.MapImageEndpoints();
app.MapHub<LogsHub>(HubRoutes.Logs);

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
