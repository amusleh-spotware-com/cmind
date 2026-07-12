using System.Text.Json;
using Core.Ai;
using Core.Ai.CurrencyStrength;
using Core.Constants;
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
    CurrencyMacroParser parser,
    IAiFeatureService ai,
    ICurrencyStrengthSnapshots snapshots,
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

        var strengthCalculator = new CurrencyStrengthCalculator();
        var forwardCalculator = new ForwardOutlookCalculator();
        var ranking = strengthCalculator.Compute(panel, StrengthWeights.Default(), asOf);
        var forwardWeights = ForwardWeights.Default();

        var horizonLayers = new Dictionary<string, HorizonLayer>(StringComparer.Ordinal);
        PairOutlookMatrix? defaultMatrix = null;
        foreach (var horizon in Horizons)
        {
            var (forecasts, matrix) = forwardCalculator.Project(ranking, gather.Trajectories, forwardWeights, horizon, asOf);
            horizonLayers[horizon.Label()] = CurrencyStrengthMapper.ToLayer(forecasts, matrix);
            if (horizon == Horizon.ThreeMonths) defaultMatrix = matrix;
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
        LogMessages.CurrencyStrengthRefreshed(logger, source.ToString(), universe.Currencies.Count);
        _ = defaultMatrix;
        return snapshot;
    }

    private async Task<IReadOnlyDictionary<string, CurrencyMacroInputs>> AssembleCalendarAsync(
        CurrencyUniverse universe, DateTimeOffset asOf, CancellationToken ct)
    {
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

        return parser.Parse(result.Text, universe);
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
