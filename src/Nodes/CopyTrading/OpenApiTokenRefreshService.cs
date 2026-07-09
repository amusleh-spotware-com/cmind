using System.Text;
using Core;
using Core.Constants;
using Core.Domain;
using Core.Logging;
using Core.Options;
using CTraderOpenApi.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nodes.CopyTrading;

/// <summary>
/// Sole authority for cTrader Open API access-token rotation. Each cycle it refreshes every
/// authorization whose access token expires within the configured threshold, persists the rotated
/// refresh token, and raises <see cref="AccessTokenRefreshed"/> so running copy operations can swap
/// the token before it expires. Gated on <see cref="OpenApiOptions.Enabled"/>.
/// </summary>
public sealed class OpenApiTokenRefreshService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    ILogger<OpenApiTokenRefreshService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.CurrentValue.OpenApi;
            if (settings.Enabled)
            {
                try
                {
                    await RefreshCycleAsync(settings, stoppingToken);
                }
                catch (Exception ex)
                {
                    log.OpenApiTokenRefreshCycleFailed(ex);
                }
            }

            await Task.Delay(options.CurrentValue.OpenApi.TokenRefreshInterval, stoppingToken);
        }
    }

    private async Task RefreshCycleAsync(OpenApiOptions settings, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenApiAuthorizationRepository>();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenApiApplicationRepository>();
        var tokenClient = scope.ServiceProvider.GetRequiredService<IOpenApiTokenClient>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var cutoff = DateTimeOffset.UtcNow + settings.TokenRefreshThreshold;
        var expiring = await authorizations.GetExpiringAsync(cutoff, ct);

        foreach (var authorization in expiring)
        {
            var application = await applications.GetByIdAsync(authorization.ApplicationId, authorization.UserId, ct);
            if (application is null)
            {
                log.OpenApiTokenRefreshApplicationMissing(
                    authorization.CtidTraderAccountId, authorization.ApplicationId.Value);
                continue;
            }

            try
            {
                var clientSecret = Encoding.UTF8.GetString(
                    protector.Unprotect(application.EncryptedClientSecret, EncryptionPurposes.OpenApiClientSecret));
                var refreshToken = Encoding.UTF8.GetString(
                    protector.Unprotect(authorization.EncryptedRefreshToken, EncryptionPurposes.OpenApiRefreshToken));

                var response = await tokenClient.RefreshAsync(application.ClientId, clientSecret, refreshToken, ct);

                var encryptedAccess = protector.Protect(
                    Encoding.UTF8.GetBytes(response.AccessToken), EncryptionPurposes.OpenApiAccessToken);
                var encryptedRefresh = protector.Protect(
                    Encoding.UTF8.GetBytes(response.RefreshToken), EncryptionPurposes.OpenApiRefreshToken);

                authorization.Refresh(
                    encryptedAccess, encryptedRefresh, DateTimeOffset.UtcNow.AddSeconds(response.ExpiresInSeconds));
                log.OpenApiTokenRefreshed(authorization.CtidTraderAccountId);
            }
            catch (Exception ex)
            {
                authorization.MarkRefreshFailed(ex.Message);
                log.OpenApiTokenRefreshFailedFor(authorization.CtidTraderAccountId, ex.Message);
            }

            await authorizations.SaveChangesAsync(ct);
        }
    }
}
