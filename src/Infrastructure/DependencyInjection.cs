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
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Core.Domain.IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<Core.Domain.IDomainEventHandler<Core.Domain.PropFirmChallengePassed>,
            Infrastructure.PropFirm.PropFirmAlertNotifier>();
        services.AddScoped<Core.Domain.IDomainEventHandler<Core.Domain.PropFirmChallengeBreached>,
            Infrastructure.PropFirm.PropFirmAlertNotifier>();
        services.AddScoped<Core.Domain.IDomainEventHandler<Core.Domain.PropFirmDrawdownWarning>,
            Infrastructure.PropFirm.PropFirmAlertNotifier>();
        services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor,
            Infrastructure.Persistence.DomainEventDispatchInterceptor>();
        services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor,
            Infrastructure.Persistence.AuditStampingInterceptor>();
        services.AddSingleton<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor,
            Infrastructure.Persistence.AuditChainInterceptor>();
        services.AddScoped<Core.Domain.IAppUserRepository, AppUserRepository>();
        services.AddScoped<Core.Domain.ICTraderIdAccountRepository, CTraderIdAccountRepository>();
        services.AddScoped<Core.Domain.IMcpApiKeyRepository, McpApiKeyRepository>();
        services.AddScoped<Core.Domain.ICBotRepository, CBotRepository>();
        services.AddScoped<Core.Domain.IAgentMandateRepository, AgentMandateRepository>();
        services.AddScoped<Core.Domain.IAlertRuleRepository, AlertRuleRepository>();
        services.AddScoped<Core.Domain.IPropRuleRepository, PropRuleRepository>();
        services.AddScoped<Core.Domain.IPropFirmChallengeRepository, PropFirmChallengeRepository>();
        services.AddScoped<Core.Domain.ILegalDocumentRepository, LegalDocumentRepository>();
        services.AddScoped<Core.Domain.IConsentRepository, ConsentRepository>();
        services.AddScoped<Core.Domain.IAuditTrailVerifier, Infrastructure.Persistence.AuditTrailVerifier>();
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
        services.AddSingleton<Core.Quant.IBacktestIntegrityAnalyzer, Core.Quant.BacktestIntegrityAnalyzer>();
        services.AddSingleton<Core.Portfolio.IPositionSizer, Core.Portfolio.PositionSizer>();
        services.AddSingleton<Core.Portfolio.IPortfolioAllocator, Core.Portfolio.PortfolioAllocator>();
        services.AddSingleton<Core.Health.IStrategyHealthMonitor, Core.Health.StrategyHealthMonitor>();
        services.AddSingleton<Core.Regimes.IRegimeAnalyzer, Core.Regimes.RegimeAnalyzer>();
        services.AddSingleton<Core.Execution.ITransactionCostAnalyzer, Core.Execution.TransactionCostAnalyzer>();
        services.AddSingleton<Core.Execution.IExecutionScheduler, Core.Execution.AlmgrenChrissScheduler>();
        services.AddSingleton<Core.Journal.IJournalAnalyzer, Core.Journal.JournalAnalyzer>();
        services.AddHttpClient<CTraderOpenApi.Auth.IOpenApiTokenClient, CTraderOpenApi.Auth.OpenApiTokenClient>(
            (sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptionsMonitor<AppOptions>>().CurrentValue.OpenApi;
                client.BaseAddress = new Uri(settings.AuthBaseUrl);
            });
        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITotpAuthenticator, OtpNetTotpAuthenticator>();
        services.AddMemoryCache();
        services.AddScoped<Core.Features.IFeatureGate, Infrastructure.Features.FeatureGate>();
        services.AddHttpClient<IGithubContainerRegistryTagProvider, GithubContainerRegistryTagProvider>();
        services.AddScoped<CBotBuilder>();
        services.AddScoped<IAiKeyStore, AiKeyStore>();
        services.AddAiHttpClient();
        services.AddScoped<IAiFeatureService, AiFeatureService>();
        return services;
    }
}
