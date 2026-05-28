using System.Security.Cryptography.X509Certificates;
using Core;
using Core.Constants;
using Core.Options;
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
    private const string DataProtectionApplicationName = "ctw";

    public static IServiceCollection AddCtwInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var dp = services.AddDataProtection().SetApplicationName(DataProtectionApplicationName);
        dp.PersistKeysToDbContext<DataContext>();

        var ctwSection = config.GetSection(AppOptions.SectionName);
        var certB64 = ctwSection[nameof(AppOptions.DataProtectionCertBase64)];
        var certPass = ctwSection[nameof(AppOptions.DataProtectionCertPassword)];
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
        return services;
    }
}
