using System.Security.Cryptography.X509Certificates;
using Core;
using Core.Constants;
using Core.Options;
using Infrastructure.Ghcr;
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
        services.AddDbContext<CtwDbContext>(o =>
            o.UseNpgsql(config.GetConnectionString(ConnectionStrings.CtwDb)));

        var dp = services.AddDataProtection().SetApplicationName(DataProtectionApplicationName);
        dp.PersistKeysToDbContext<CtwDbContext>();

        var ctwSection = config.GetSection(CtwOptions.SectionName);
        var certB64 = ctwSection[nameof(CtwOptions.DataProtectionCertBase64)];
        var certPass = ctwSection[nameof(CtwOptions.DataProtectionCertPassword)];
        if (!string.IsNullOrWhiteSpace(certB64))
        {
            var bytes = Convert.FromBase64String(certB64);
            var cert = X509CertificateLoader.LoadPkcs12(bytes, certPass);
            dp.ProtectKeysWithCertificate(cert);
        }

        services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddMemoryCache();
        services.AddHttpClient<IGhcrTagProvider, GhcrTagProvider>();
        return services;
    }
}
