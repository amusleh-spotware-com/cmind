namespace Core.Domain;

public interface ICopySizingCalculator
{
    CopyVolume Calculate(CopySizingInput input);
}

public sealed class CopySizingCalculator : ICopySizingCalculator
{
    public CopyVolume Calculate(CopySizingInput input)
    {
        var raw = RawVolume(input);
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
            _ => 0
        };
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
