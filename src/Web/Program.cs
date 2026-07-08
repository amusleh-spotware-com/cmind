using System.Threading.RateLimiting;
using Core;
using Core.Constants;
using Core.NodeAgent;
using Core.Options;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Components;
using Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using MudBlazor.Services;
using Nodes;
using Serilog;
using Web.Auth;
using Web.Components;
using Web.Endpoints;
using Web.Hubs;
using Web.Security;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservabilityDefaults();
builder.AddNpgsqlDbContext<DataContext>(ConnectionStrings.AppDb);

builder.Services
    .AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddNodes();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => builder.Configuration.GetConnectionString(ConnectionStrings.AppDb)
                     ?? throw new InvalidOperationException("Missing connection string"),
        name: "postgres", tags: ["ready"]);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddMudServices();
builder.Services.AddSignalR();
if (builder.Environment.IsDevelopment())
    builder.Services.AddOpenApi();
builder.Services.AddAntiforgery();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimitPolicies.Auth, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RateLimitPolicies.AuthPermitPerWindow,
                Window = TimeSpan.FromSeconds(RateLimitPolicies.AuthWindowSeconds)
            }));
});
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new StrongIdJsonConverterFactory()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/forbidden";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.Owner, p => p.RequireRole("Owner"))
    .AddPolicy(AuthPolicies.AdminOrAbove, p => p.RequireRole("Owner", "Admin"))
    .AddPolicy(AuthPolicies.UserOrAbove, p => p.RequireRole("Owner", "Admin", "User"));

builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CookieForwardingHandler>();
builder.Services.AddHttpClient("self")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
    .AddHttpMessageHandler<CookieForwardingHandler>();
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var http = factory.CreateClient("self");
    http.BaseAddress = new Uri(nav.BaseUri);
    return http;
});
builder.Services.AddHostedService<OwnerSeeder>();
builder.Services.AddHostedService<LocalNodeSeeder>();
builder.Services.AddHostedService<InstanceReconciler>();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.MapHostHealthEndpoints();
app.MapGet(HealthEndpoints.Version, () => Results.Ok(new NodeAgentInfoResponse(VersionInfo.Product, NodeAgentProtocol.Version)))
    .AllowAnonymous();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseSecurityHeaders();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
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
app.MapDashboardEndpoints();
app.MapAiEndpoints();
app.MapAgentEndpoints();
app.MapAlertEndpoints();
app.MapPropGuardEndpoints();
app.MapImageEndpoints();
app.MapHub<LogsHub>(HubRoutes.Logs);

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
