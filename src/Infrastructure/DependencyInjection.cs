using System.Security.Cryptography.X509Certificates;
using Core;
using Core.Ai;
using Core.Constants;
using Core.Options;
using Infrastructure.Ai;
using Infrastructure.Builder;
using Infrastructure.Github;
using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    private const string DataProtectionApplicationName = "app";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var dp = services.AddDataProtection().SetApplicationName(DataProtectionApplicationName);
        dp.PersistKeysToDbContext<DataContext>();

        var appSection = config.GetSection(AppOptions.SectionName);
        var certB64 = appSection[nameof(AppOptions.DataProtectionCertBase64)];
        var certPass = appSection[nameof(AppOptions.DataProtectionCertPassword)];
        if (!string.IsNullOrWhiteSpace(certB64))
        {
            var bytes = Convert.FromBase64String(certB64);
            var cert = X509CertificateLoader.LoadPkcs12(bytes, certPass);
            dp.ProtectKeysWithCertificate(cert);
        }

        services.AddScoped<Core.Domain.IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor,
            Infrastructure.Persistence.DomainEventDispatchInterceptor>();
        services.AddScoped<Core.Domain.IAppUserRepository, AppUserRepository>();
        services.AddScoped<Core.Domain.ICTraderIdAccountRepository, CTraderIdAccountRepository>();
        services.AddScoped<Core.Domain.IMcpApiKeyRepository, McpApiKeyRepository>();
        services.AddScoped<Core.Domain.ICBotRepository, CBotRepository>();
        services.AddScoped<Core.Domain.IAgentMandateRepository, AgentMandateRepository>();
        services.AddScoped<Core.Domain.IAlertRuleRepository, AlertRuleRepository>();
        services.AddScoped<Core.Domain.IPropRuleRepository, PropRuleRepository>();
        services.AddScoped<Core.Domain.IOpenApiApplicationRepository, OpenApiApplicationRepository>();
        services.AddScoped<Core.Domain.IOpenApiAuthorizationRepository, OpenApiAuthorizationRepository>();
        services.AddSingleton<CTraderOpenApi.Transport.IOpenApiTransportFactory,
            CTraderOpenApi.Transport.TcpSslOpenApiTransportFactory>();
        services.AddSingleton<CTraderOpenApi.Client.IOpenApiConnectionFactory,
            Infrastructure.OpenApi.OpenApiConnectionFactory>();
        services.AddSingleton<CTraderOpenApi.Client.IOpenApiTradingSessionFactory,
            CTraderOpenApi.Client.OpenApiTradingSessionFactory>();
        services.AddScoped<CTraderOpenApi.Client.IOpenApiClient, CTraderOpenApi.Client.OpenApiClient>();
        services.AddSingleton<Core.Domain.IOAuthStateService, Infrastructure.OpenApi.OAuthStateService>();
        services.AddScoped<Core.Domain.ICopyProfileRepository, CopyProfileRepository>();
        services.AddSingleton<Core.Domain.ICopySizingCalculator, Core.Domain.CopySizingCalculator>();
        services.AddHttpClient<CTraderOpenApi.Auth.IOpenApiTokenClient, CTraderOpenApi.Auth.OpenApiTokenClient>(
            (sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptionsMonitor<AppOptions>>().CurrentValue.OpenApi;
                client.BaseAddress = new Uri(settings.AuthBaseUrl);
            });
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddMemoryCache();
        services.AddHttpClient<IGithubContainerRegistryTagProvider, GithubContainerRegistryTagProvider>();
        services.AddScoped<CBotBuilder>();
        services.AddHttpClient<IAiClient, AnthropicAiClient>();
        services.AddScoped<IAiFeatureService, AiFeatureService>();
        return services;
    }
}
