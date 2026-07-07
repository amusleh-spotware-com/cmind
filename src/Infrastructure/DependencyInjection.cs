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
