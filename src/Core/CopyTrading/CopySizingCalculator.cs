namespace Core.Domain;

public interface ICopySizingCalculator
{
    CopyVolume Calculate(CopySizingInput input);
}

public sealed class CopySizingCalculator : ICopySizingCalculator
{
    public CopyVolume Calculate(CopySizingInput input)
    {
        // The per-symbol volume multiplier scales the sized result, then bounds (min/max lot) are applied to
        // the scaled value — and the decision engine's lot-sanity ceiling also sees the scaled result — so a
        // per-symbol override can't smuggle a runaway size past those guards.
        var raw = RawVolume(input) * input.VolumeMultiplier;
        if (raw <= 0 || double.IsNaN(raw) || double.IsInfinity(raw)) return CopyVolume.Skip;
        return Normalize(raw, input.DestinationSymbol, input.Bounds);
    }

    private static double RawVolume(CopySizingInput input)
    {
        var master = input.MasterVolumeLots;
        var risk = input.Risk;
        var destination = input.Destination;
        var masterSpec = input.MasterSymbol;
        var destinationSpec = input.DestinationSymbol;

        return risk.Mode switch
        {
            MoneyManagementMode.FixedLot => risk.Parameter,
            MoneyManagementMode.LotMultiplier => master * risk.Parameter,
            MoneyManagementMode.NotionalMultiplier =>
                master * Ratio(masterSpec.ContractSize, destinationSpec.ContractSize) * risk.Parameter,
            MoneyManagementMode.ProportionalBalance =>
                master * Ratio(destination.Balance, input.Master.Balance) * risk.Parameter,
            MoneyManagementMode.ProportionalEquity =>
                master * Ratio(destination.Equity, input.Master.Equity) * risk.Parameter,
            MoneyManagementMode.ProportionalFreeMargin =>
                master * Ratio(destination.FreeMargin, input.Master.FreeMargin) * risk.Parameter,
            MoneyManagementMode.AutoProportional =>
                master * Ratio(destination.Balance, input.Master.Balance) * risk.Parameter,
            MoneyManagementMode.FixedRiskPercent =>
                Notional(destination.Balance * risk.Parameter / 100.0, destinationSpec.ContractSize),
            MoneyManagementMode.FixedLeverage =>
                Notional(destination.Balance * risk.Parameter, destinationSpec.ContractSize),
            MoneyManagementMode.RiskFromStopLoss => RiskFromStop(input),
            _ => 0
        };
    }

    // M7: size the destination so it risks the same percent of ITS balance as configured, derived from the
    // master's stop-loss distance — "master risks 2% -> slave auto-risks 2%". lots = riskAmount / lossPerLot
    // where lossPerLot = stopDistance (price) x contract size. Quote==deposit assumed (no FX conversion feed),
    // matching the equity calculator's documented simplification. No master SL -> 0 (the engine skips it).
    private static double RiskFromStop(CopySizingInput input)
    {
        // No master stop-loss -> use the configured max-risk fallback lot (0 falls through to a skip).
        if (input.MasterStopDistance <= 0) return input.RiskFallbackLots;
        var riskAmount = input.Destination.Balance * input.Risk.Parameter / 100.0;
        var lossPerLot = input.MasterStopDistance * input.DestinationSymbol.ContractSize;
        return lossPerLot <= 0 ? 0 : riskAmount / lossPerLot;
    }

    private static double Ratio(double numerator, double denominator)
        => denominator <= 0 ? 0 : numerator / denominator;

    private static double Notional(double exposure, double contractSize)
        => contractSize <= 0 ? 0 : exposure / contractSize;

    private static CopyVolume Normalize(double raw, SymbolSpec spec, LotBounds bounds)
    {
        var volume = raw;

        if (spec.LotStep > 0)
            volume = Math.Floor(volume / spec.LotStep) * spec.LotStep;

        var minLot = Math.Max(bounds.MinLot, spec.MinLot);
        if (volume < minLot)
        {
            if (!bounds.ForceMinLot || minLot <= 0) return CopyVolume.Skip;
            volume = minLot;
        }

        var maxLot = SmallestPositive(bounds.MaxLot, spec.MaxLot);
        if (maxLot > 0 && volume > maxLot)
            volume = maxLot;

        return volume <= 0 ? CopyVolume.Skip : new CopyVolume(volume, false);
    }

    private static double SmallestPositive(double first, double second)
    {
        if (first <= 0) return second;
        if (second <= 0) return first;
        return Math.Min(first, second);
    }
}
