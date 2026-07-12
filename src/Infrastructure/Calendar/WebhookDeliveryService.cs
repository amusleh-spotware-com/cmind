using System.Globalization;
using System.Text;
using System.Text.Json;
using Core;
using Core.Calendar;
using Core.Constants;
using Core.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Infrastructure.Calendar;

/// <summary>
/// Delivers newly-released events to registered webhooks, HMAC-signed. Poll-backed off a persisted watermark
/// (max delivered <c>KnownAt</c>) so it survives restarts and never re-delivers; gated on
/// <see cref="CalendarOptions.WebhooksEnabled"/> (off by default — it makes external calls). Best-effort per
/// delivery; a failed POST is simply retried on a later cycle when the watermark has not advanced past it.
/// </summary>
public sealed class WebhookDeliveryService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider)
    : BackgroundService
{
    private const string WatermarkSetting = "calendar.webhook.watermark";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.CurrentValue.Calendar.WebhooksEnabled) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { /* isolate the worker from reads; retry next cycle */ }

            try { await Task.Delay(options.CurrentValue.Calendar.WebhookPollInterval, timeProvider, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var delivery = scope.ServiceProvider.GetRequiredService<WebhookDelivery>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        var webhooks = await db.CalendarWebhooks.Where(w => w.DisabledAt == null).ToListAsync(ct);
        if (webhooks.Count == 0) return;

        var now = timeProvider.GetUtcNow();
        var watermark = await ReadWatermarkAsync(db, now, ct);

        var recent = await db.EconomicEvents.AsNoTracking()
            .Where(e => e.EffectiveAt >= now.AddDays(-2) && e.EffectiveAt <= now)
            .ToListAsync(ct);

        var newReleases = recent
            .Where(e => e.LatestRevision is { Actual: not null } latest && latest.KnownAt > watermark)
            .OrderBy(e => e.LatestRevision!.KnownAt)
            .ToList();
        if (newReleases.Count == 0) return;

        foreach (var economicEvent in newReleases)
        {
            var impact = economicEvent.ImpactLevelAsOf(now);
            var currencies = CurrencyExposure.CurrenciesOf(economicEvent.Country).Select(c => c.Value).ToList();
            var latest = economicEvent.LatestRevision!;
            var payload = JsonSerializer.Serialize(new
            {
                id = economicEvent.Id.Value,
                seriesCode = economicEvent.SeriesCodeValue,
                country = economicEvent.CountryValue,
                effectiveAt = economicEvent.EffectiveAt,
                actual = latest.Actual,
                impact = impact.ToString()
            });

            foreach (var webhook in webhooks.Where(w => w.Matches(impact, currencies)))
            {
                var secret = Encoding.UTF8.GetString(
                    protector.Unprotect(webhook.EncryptedSecret, EncryptionPurposes.CalendarWebhookSecret));
                await delivery.DeliverAsync(webhook.Url, secret, payload, ct);
            }
        }

        var maxKnownAt = newReleases.Max(e => e.LatestRevision!.KnownAt);
        await WriteWatermarkAsync(db, maxKnownAt, now, ct);
    }

    private static async Task<DateTimeOffset> ReadWatermarkAsync(DataContext db, DateTimeOffset now, CancellationToken ct)
    {
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == WatermarkSetting, ct);
        return setting is not null
               && DateTimeOffset.TryParse(setting.Value, CultureInfo.InvariantCulture,
                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var value)
            ? value
            : now.AddHours(-1);
    }

    private static async Task WriteWatermarkAsync(DataContext db, DateTimeOffset watermark, DateTimeOffset now, CancellationToken ct)
    {
        var value = watermark.ToString("O", CultureInfo.InvariantCulture);
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == WatermarkSetting, ct);
        if (setting is null) db.AppSettings.Add(AppSetting.Create(WatermarkSetting, value, now));
        else setting.SetValue(value, now);
        await db.SaveChangesAsync(ct);
    }
}
