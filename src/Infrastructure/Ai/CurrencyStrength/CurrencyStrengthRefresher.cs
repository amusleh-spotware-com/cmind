using System.Text.Json;
using Core.Ai;
using Core.Ai.CurrencyStrength;
using Core.Calendar;
using Core.Constants;
using Core.Features;
using Core.Logging;
using Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Ai.CurrencyStrength;

/// <summary>
/// Orchestrates one refresh: assemble calendar current actuals + surprises (point-in-time) → gather the AI
/// forward trajectory + geopolitics → parse + merge → <see cref="CurrencyStrengthCalculator">compute</see> the
/// current ranking → <see cref="ForwardOutlookCalculator">project</see> the pair-outlook matrix for every
/// horizon → explain → persist one snapshot. The worker orchestrates; the domain decides. Degrades layer by
/// layer: calendar-off ⇒ AI-only current figures; AI-off ⇒ calendar-only ranking (no forward projection);
/// both off ⇒ no snapshot.
/// </summary>
public sealed class CurrencyStrengthRefresher(
    CurrencyMacroAssembler assembler,
    IAiFeatureService ai,
    ICurrencyStrengthSnapshots snapshots,
    IFeatureGate featureGate,
    IOptionsMonitor<AppOptions> options,
    TimeProvider timeProvider,
    ILogger<CurrencyStrengthRefresher> logger)
{
    private static readonly Horizon[] Horizons =
        [Horizon.OneMonth, Horizon.ThreeMonths, Horizon.SixMonths, Horizon.TwelveMonths];

    public async Task<CurrencyStrengthSnapshot?> RefreshAsync(CancellationToken ct)
    {
        var asOf = timeProvider.GetUtcNow();
        var universe = CurrencyUniverseFactory.FromConfig(options.CurrentValue.CurrencyStrength);

        var calendarInputs = await AssembleCalendarAsync(universe, asOf, ct);
        var gather = await GatherForwardAsync(universe, calendarInputs, ct);

        var hasCalendar = calendarInputs.Count > 0;
        var hasAi = gather.HasForward || gather.GapFill.Count > 0;
        if (!hasCalendar && !hasAi)
        {
            LogMessages.CurrencyStrengthNoData(logger);
            return null;
        }

        var panel = universe.Currencies
            .Select(c => CurrencyMacroBinder.Merge(
                c,
                calendarInputs.GetValueOrDefault(c.Code),
                gather.GapFill.GetValueOrDefault(c.Code)))
            .ToList();

        var ranking = CurrencyStrengthCalculator.Compute(panel, StrengthWeights.Default(), asOf);
        var forwardWeights = ForwardWeights.Default();

        var horizonLayers = new Dictionary<string, HorizonLayer>(StringComparer.Ordinal);
        foreach (var horizon in Horizons)
        {
            var (forecasts, matrix) = ForwardOutlookCalculator.Project(ranking, gather.Trajectories, forwardWeights, horizon, asOf);
            horizonLayers[horizon.Label()] = CurrencyStrengthMapper.ToLayer(forecasts, matrix);
        }

        var rankRows = CurrencyStrengthMapper.ToRankRows(ranking);
        var narrative = await ExplainAsync(rankRows, horizonLayers, ct);
        var source = (hasCalendar, hasAi) switch
        {
            (true, true) => SnapshotSource.CalendarAndAi,
            (true, false) => SnapshotSource.CalendarOnly,
            _ => SnapshotSource.AiOnly
        };

        var snapshot = CurrencyStrengthSnapshot.Create(
            asOf,
            Serialize(rankRows),
            Serialize(horizonLayers),
            Serialize(panel.Select(p => new { p.Currency.Code, p.PolicyRate, p.Cpi, p.GdpGrowth, p.Unemployment, Provenance = p.Provenance.ToString() })),
            narrative,
            source,
            hasCalendar ? asOf : null);

        await snapshots.AddAsync(snapshot, ct);
        LogMessages.CurrencyStrengthRefreshed(logger, source, universe.Currencies.Count);
        return snapshot;
    }

    private async Task<IReadOnlyDictionary<string, CurrencyMacroInputs>> AssembleCalendarAsync(
        CurrencyUniverse universe, DateTimeOffset asOf, CancellationToken ct)
    {
        // Respect the calendar's white-label hard gate + runtime toggle — a calendar-off deployment falls
        // back to AI-only figures rather than reading the calendar read model.
        if (!CalendarEnablement.IsEnabled(options.CurrentValue.Branding, featureGate))
            return new Dictionary<string, CurrencyMacroInputs>(StringComparer.Ordinal);

        try
        {
            return await assembler.AssembleAsync(universe, asOf, ct);
        }
        catch (Exception ex)
        {
            LogMessages.CurrencyStrengthCalendarUnavailable(logger, ex);
            return new Dictionary<string, CurrencyMacroInputs>(StringComparer.Ordinal);
        }
    }

    private async Task<CurrencyForwardGather> GatherForwardAsync(
        CurrencyUniverse universe, IReadOnlyDictionary<string, CurrencyMacroInputs> calendarInputs, CancellationToken ct)
    {
        if (!ai.Enabled) return CurrencyForwardGather.Empty;

        var context = CurrencyMacroAssembler.BuildContextJson(universe, calendarInputs);
        var budget = Math.Min(
            AiConstants.CurrencyStrengthGatherMaxTokens,
            AiConstants.CurrencyStrengthGatherBaseTokens + AiConstants.CurrencyStrengthGatherTokensPerCurrency * universe.Currencies.Count);

        var result = await ai.GatherCurrencyForwardAsync(context, budget, ct);
        if (!result.Success)
        {
            LogMessages.CurrencyStrengthGatherFailed(logger, result.Error ?? "unknown");
            return CurrencyForwardGather.Empty;
        }

        return CurrencyMacroParser.Parse(result.Text, universe);
    }

    private async Task<string> ExplainAsync(
        IReadOnlyList<RankRow> ranking, IReadOnlyDictionary<string, HorizonLayer> horizons, CancellationToken ct)
    {
        if (!ai.Enabled) return string.Empty;
        var matrix = horizons.GetValueOrDefault(Horizon.ThreeMonths.Label())?.Pairs ?? [];
        var result = await ai.ExplainCurrencyOutlookAsync(
            Serialize(ranking), Serialize(matrix), AiConstants.CurrencyStrengthExplainMaxTokens, ct);
        return result.Success ? result.Text : string.Empty;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, CurrencyStrengthJson.Options);
}
