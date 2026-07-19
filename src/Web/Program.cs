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
builder.AddNpgsqlDbContext<DataContext>(ConnectionStrings.AppDb, settings =>
{
    settings.DisableRetry = false;
    settings.CommandTimeout = DatabaseDefaults.CommandTimeoutSeconds;
});

builder.Services
    .AddOptions<AppOptions>()
    .Bind(builder.Configuration.GetSection(AppOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddNodes(builder.Configuration);
builder.Services.AddScoped<Web.Calendar.CalendarJwt>();

builder.Services.AddHealthChecks()
    .AddNpgSql(sp => builder.Configuration.GetConnectionString(ConnectionStrings.AppDb)
                     ?? throw new InvalidOperationException("Missing connection string"),
        name: "postgres", tags: ["ready"]);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());
builder.Services.AddMudServices();

// Global mapping of domain/persistence failures on /api routes to RFC7807 ProblemDetails with the
// correct status (DomainException → 400, unique-violation → 409) instead of a raw 500. See
// Web.Security.DomainExceptionMiddleware (registered first in the pipeline below).
builder.Services.AddProblemDetails();

// Localization: resources live in src/Web/Resources (Ui.resx + one Ui.<culture>.resx per language).
// The request-culture pipeline (below) picks the culture from the cookie the switcher/login writes,
// then the browser's Accept-Language on a first visit, falling back to English.
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(o =>
{
    var supported = SupportedCultures.All
        .Select(c => new System.Globalization.CultureInfo(c))
        .ToArray();
    o.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(SupportedCultures.Default);
    o.SupportedCultures = supported;
    o.SupportedUICultures = supported;
    o.FallBackToParentUICultures = true;
    o.ApplyCurrentCultureToResponseHeaders = true;
    o.RequestCultureProviders =
    [
        new Microsoft.AspNetCore.Localization.CookieRequestCultureProvider(),
        new Microsoft.AspNetCore.Localization.AcceptLanguageHeaderRequestCultureProvider()
    ];
});
// S6 Web scale-out: with multiple Web replicas, a Redis backplane fans SignalR hub messages (logs hub,
// Blazor Server negotiation) across replicas so a circuit reconnecting to a different pod stays live.
// Absent connection string => single-replica in-memory (unchanged).
var signalR = builder.Services.AddSignalR();
var signalRBackplane = builder.Configuration.GetConnectionString("signalr");
if (!string.IsNullOrWhiteSpace(signalRBackplane))
    signalR.AddStackExchangeRedis(signalRBackplane);
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
    o.AddPolicy(RateLimitPolicies.Registration, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = RateLimitPolicies.RegistrationPermitPerWindow,
                Window = TimeSpan.FromSeconds(RateLimitPolicies.RegistrationWindowSeconds)
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

builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<AppOptions>, Web.Branding.BrandingOptionsValidator>();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<AppOptions>, Web.Registration.RegistrationOptionsValidator>();
builder.Services.AddSingleton<Web.Branding.IBrandingThemeProvider, Web.Branding.BrandingThemeProvider>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<Web.Time.IUserTimeZone, Web.Time.UserTimeZone>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<CookieForwardingHandler>();
builder.Services.AddScoped<Web.Hubs.LogHubConnectionFactory>();
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
builder.Services.AddScoped<Web.OpenApi.OpenApiAccountLinker>();
builder.Services.AddScoped<Core.Accounts.IBrokerVerifier, Web.Accounts.BrokerVerifier>();
builder.Services.AddSingleton<OwnerSeeder>();
builder.Services.AddHostedService<LocalNodeSeeder>();
builder.Services.AddHostedService<InstanceReconciler>();

var app = builder.Build();

// Apply migrations + first-run seeding synchronously BEFORE the host serves requests or starts background
// services, so nothing (settings readers, the DataProtection keyring, node/instance pollers) ever queries
// a not-yet-created schema on a fresh database. Runs under the shared advisory lock (safe across replicas).
await app.Services.GetRequiredService<OwnerSeeder>().InitializeAsync(default);

// FIRST in the pipeline so it catches /api domain/persistence failures before the developer exception
// page (auto-added in Development) or the /error page — the /api ProblemDetails contract holds in every
// environment. Non-/api and unclassified exceptions rethrow to the handlers below.
app.UseMiddleware<Web.Security.DomainExceptionMiddleware>();

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
app.UseRequestLocalization();
app.UseMiddleware<Web.Security.MfaEnforcementMiddleware>();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();
app.MapAuthEndpoints();
app.MapLocalizationEndpoints();
app.MapTimeZoneEndpoints();
app.MapRegistrationEndpoints();
app.MapCBotEndpoints();
app.MapNodeEndpoints();
app.MapInstanceEndpoints();
app.MapUserEndpoints();
app.MapCtidEndpoints();
app.MapOpenApiEndpoints();
app.MapCopyEndpoints();
app.MapParamSetEndpoints();
app.MapMcpKeyEndpoints();
app.MapCalendarApiEndpoints();
app.MapBuilderEndpoints();
app.MapDashboardEndpoints();
app.MapUsageEndpoints();
app.MapFeatureEndpoints();
app.MapWhiteLabelSettingsEndpoints();
app.MapAiEndpoints();
app.MapCurrencyStrengthEndpoints();
app.MapCotApiEndpoints();
app.MapQuantEndpoints();
app.MapAgentStudioEndpoints();
app.MapJournalEndpoints();
app.MapAgentEndpoints();
app.MapAlertEndpoints();
app.MapPropGuardEndpoints();
app.MapPropFirmEndpoints();
// Dev-only, config-gated test seeding — never mapped in Production (guarded twice). Fail-fast if the
// flag is ever set outside Development so a prod misconfiguration crashes loudly instead of silently.
if (app.Configuration.GetValue<bool>(TestSeedEndpoints.EnabledKey))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException($"{TestSeedEndpoints.EnabledKey} must never be enabled outside Development.");
    app.MapTestSeedEndpoints();
}
app.MapComplianceEndpoints();
app.MapImageEndpoints();
app.MapPwaEndpoints();
app.MapHub<LogsHub>(HubRoutes.Logs);

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

public partial class Program;
