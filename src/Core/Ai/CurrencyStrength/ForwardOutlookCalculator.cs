using Core.Constants;
using Core.Domain;

namespace Core.Ai.CurrencyStrength;

/// <summary>
/// The deterministic forward engine (the headline layer). Carries each currency's current composite along its
/// expected trajectory — <c>projected = current + horizonScale · Σ trajectoryDriver·weight</c> — then maps the
/// per-pair projected differential to a directional bias through a tier-aware dead-band. Pure: no I/O, no
/// clock, no LLM. Longer horizons weigh the trajectory more; pegged currencies clamp toward Neutral.
/// </summary>
public static class ForwardOutlookCalculator
{
    private const double PegTrajectoryFactor = 0.25;
    private const double ConvictionScale = 2.0;

    public static (IReadOnlyList<CurrencyForecast> Forecasts, PairOutlookMatrix Matrix) Project(
        CurrencyStrengthRanking current,
        IReadOnlyList<CurrencyTrajectory> trajectories,
        ForwardWeights weights,
        Horizon horizon,
        DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(trajectories);
        ArgumentNullException.ThrowIfNull(weights);

        var trajByCode = trajectories.ToDictionary(t => t.Currency.Code, StringComparer.Ordinal);
        var scale = horizon.Scale();

        // Within-tier z of each trajectory driver.
        var zByCode = new Dictionary<string, Dictionary<MacroDriver, double>>(StringComparer.Ordinal);
        foreach (var t in trajectories)
            zByCode[t.Currency.Code] = [];

        foreach (var tierGroup in trajectories.GroupBy(t => t.Currency.Tier))
        {
            var members = tierGroup.ToList();
            var tierWeights = weights.ForTier(tierGroup.Key);
            foreach (var driver in tierWeights.Keys)
            {
                var raws = members.Select(m => ForwardRaw(driver, m)).ToList();
                var zs = Normalization.ZScores(raws, CurrencyStrengthCalculator.WinsorLimit);
                for (var i = 0; i < members.Count; i++)
                    zByCode[members[i].Currency.Code][driver] = zs[i];
            }
        }

        var forecasts = new List<CurrencyForecast>(current.Scores.Count);
        var forecastByCode = new Dictionary<string, CurrencyForecast>(StringComparer.Ordinal);
        foreach (var score in current.Scores)
        {
            var currency = score.Currency;
            var pegFactor = currency.IsPegged ? PegTrajectoryFactor : 1.0;
            var forwardBreakdown = new List<DriverScore>();
            var forwardComposite = 0.0;

            if (trajByCode.TryGetValue(currency.Code, out var trajectory))
            {
                var tierWeights = weights.ForTier(currency.Tier);
                foreach (var (driver, weight) in tierWeights)
                {
                    var z = zByCode[currency.Code].GetValueOrDefault(driver, 0.0);
                    var contribution = z * weight * scale * pegFactor;
                    forwardComposite += contribution;
                    forwardBreakdown.Add(new DriverScore(driver, z, weight, contribution, ForwardRationale(driver, z)));
                }
            }

            var projected = score.Composite + forwardComposite;
            var confidence = Worse(score.Confidence, trajectory?.Confidence ?? DataConfidence.Low);
            if (currency.IsPegged) confidence = DataConfidence.Low;

            var forecast = new CurrencyForecast(currency, horizon, projected, score.Composite, forwardBreakdown, confidence);
            forecasts.Add(forecast);
            forecastByCode[currency.Code] = forecast;
        }

        forecasts.Sort((a, b) =>
        {
            var byScore = b.ProjectedScore.CompareTo(a.ProjectedScore);
            return byScore != 0 ? byScore : string.CompareOrdinal(a.Currency.Code, b.Currency.Code);
        });

        var pairs = new List<PairOutlook>();
        foreach (var baseForecast in forecasts)
        foreach (var quoteForecast in forecasts)
        {
            if (string.Equals(baseForecast.Currency.Code, quoteForecast.Currency.Code, StringComparison.Ordinal))
                continue;
            pairs.Add(BuildPair(baseForecast, quoteForecast, horizon));
        }

        return (forecasts, new PairOutlookMatrix(pairs, horizon, asOf));
    }

    private static PairOutlook BuildPair(CurrencyForecast @base, CurrencyForecast quote, Horizon horizon)
    {
        var differential = @base.ProjectedScore - quote.ProjectedScore;
        var pegged = @base.Currency.IsPegged || quote.Currency.IsPegged;

        var deadBand = Math.Max(DeadBand(@base.Currency.Tier), DeadBand(quote.Currency.Tier));
        if (pegged) deadBand *= 2.0;

        var bias = differential > deadBand ? DirectionalBias.Appreciate
            : differential < -deadBand ? DirectionalBias.Depreciate
            : DirectionalBias.Neutral;

        var cap = Math.Min(ConvictionCap(@base.Currency.Tier), ConvictionCap(quote.Currency.Tier));
        if (pegged) cap *= 0.5;
        var conviction = Math.Min(cap, Math.Abs(differential) / ConvictionScale);

        var why = BuildWhy(@base, quote);
        var confidence = pegged ? DataConfidence.Low : Worse(@base.Confidence, quote.Confidence);

        return new PairOutlook(
            @base.Currency, quote.Currency, horizon, bias, conviction, differential, why, confidence, pegged);
    }

    private static List<DriverScore> BuildWhy(CurrencyForecast @base, CurrencyForecast quote)
    {
        var quoteByDriver = quote.ForwardBreakdown.ToDictionary(d => d.Driver);
        var why = new List<DriverScore>();
        foreach (var b in @base.ForwardBreakdown)
        {
            var q = quoteByDriver.GetValueOrDefault(b.Driver);
            var diff = b.Contribution - (q?.Contribution ?? 0.0);
            why.Add(new DriverScore(b.Driver, b.Normalized, b.Weight, diff, ForwardRationale(b.Driver, diff)));
        }

        return why;
    }

    /// <summary>Trajectory raw; higher ⇒ stronger forward.</summary>
    internal static double ForwardRaw(MacroDriver driver, CurrencyTrajectory t) => driver switch
    {
        MacroDriver.RateTrajectory => t.ExpectedRatePathBp,
        MacroDriver.InflationTrend => -t.InflationTrend,
        MacroDriver.GrowthMomentum => t.GrowthMomentum,
        MacroDriver.GeopoliticalRisk => t.GeopoliticalDelta,
        _ => 0.0
    };

    private static double DeadBand(CurrencyTier tier) => tier switch
    {
        CurrencyTier.Major => 0.15,
        CurrencyTier.EmergingMarket => 0.30,
        CurrencyTier.Exotic => 0.45,
        _ => 0.30
    };

    private static double ConvictionCap(CurrencyTier tier) => tier switch
    {
        CurrencyTier.Major => 1.0,
        CurrencyTier.EmergingMarket => 0.8,
        CurrencyTier.Exotic => 0.6,
        _ => 0.8
    };

    private static DataConfidence Worse(DataConfidence a, DataConfidence b) =>
        (DataConfidence)Math.Max((int)a, (int)b);

    private static string ForwardRationale(MacroDriver driver, double value)
    {
        var lean = value > 0.05 ? "forward tailwind" : value < -0.05 ? "forward headwind" : "forward-neutral";
        return $"{driver}: {lean} ({value:+0.00;-0.00;0.00}).";
    }
}
