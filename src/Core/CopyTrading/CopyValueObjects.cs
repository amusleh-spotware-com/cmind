using Core.Constants;

namespace Core.Domain;

public enum MoneyManagementMode
{
    FixedLot = 0,
    LotMultiplier = 1,
    NotionalMultiplier = 2,
    ProportionalBalance = 3,
    ProportionalEquity = 4,
    ProportionalFreeMargin = 5,
    FixedRiskPercent = 6,
    FixedLeverage = 7,
    AutoProportional = 8
}

public enum CopyDirectionFilter
{
    Both = 0,
    LongOnly = 1,
    ShortOnly = 2
}

public enum SymbolFilterMode
{
    None = 0,
    Whitelist = 1,
    Blacklist = 2
}

public enum CopyProfileStatus
{
    Draft = 0,
    Running = 1,
    Paused = 2,
    Stopped = 3,
    Error = 4
}

public readonly record struct SlippagePips
{
    public double Value { get; }

    public SlippagePips(double value)
    {
        if (value < 0 || double.IsNaN(value)) throw new DomainException(DomainErrors.CopySlippageInvalid);
        Value = value;
    }
}

public readonly record struct MaxCopyDelay
{
    public TimeSpan Value { get; }

    public MaxCopyDelay(TimeSpan value)
    {
        if (value < TimeSpan.Zero || value.TotalSeconds > int.MaxValue)
            throw new DomainException(DomainErrors.CopyDelayInvalid);
        Value = value;
    }

    public static MaxCopyDelay Seconds(int seconds) => new(TimeSpan.FromSeconds(seconds));
}

public readonly record struct LotBounds
{
    public double MinLot { get; }
    public double MaxLot { get; }
    public bool ForceMinLot { get; }

    public LotBounds(double minLot, double maxLot, bool forceMinLot)
    {
        if (minLot < 0 || maxLot < 0 || double.IsNaN(minLot) || double.IsNaN(maxLot))
            throw new DomainException(DomainErrors.CopyLotBoundsInvalid);
        if (maxLot > 0 && maxLot < minLot)
            throw new DomainException(DomainErrors.CopyLotBoundsInvalid);
        MinLot = minLot;
        MaxLot = maxLot;
        ForceMinLot = forceMinLot;
    }

    public static LotBounds Unbounded => new(0, 0, false);
}

public readonly record struct SymbolMapEntry
{
    public Symbol Source { get; }
    public Symbol Destination { get; }

    public SymbolMapEntry(Symbol source, Symbol destination)
    {
        Source = source;
        Destination = destination;
    }
}

public readonly record struct RiskSettings
{
    public MoneyManagementMode Mode { get; }
    public double Parameter { get; }

    public RiskSettings(MoneyManagementMode mode, double parameter)
    {
        Validate(mode, parameter);
        Mode = mode;
        Parameter = parameter;
    }

    private static void Validate(MoneyManagementMode mode, double parameter)
    {
        if (double.IsNaN(parameter) || double.IsInfinity(parameter))
            throw new DomainException(DomainErrors.CopyRiskParameterInvalid);

        switch (mode)
        {
            case MoneyManagementMode.FixedLot when parameter <= 0:
                throw new DomainException(DomainErrors.CopyLotInvalid);
            case MoneyManagementMode.LotMultiplier or MoneyManagementMode.NotionalMultiplier
                or MoneyManagementMode.ProportionalBalance or MoneyManagementMode.ProportionalEquity
                or MoneyManagementMode.ProportionalFreeMargin or MoneyManagementMode.AutoProportional
                when parameter <= 0:
                throw new DomainException(DomainErrors.CopyMultiplierInvalid);
            case MoneyManagementMode.FixedRiskPercent when parameter is <= 0 or > 100:
                throw new DomainException(DomainErrors.CopyRiskParameterInvalid);
            case MoneyManagementMode.FixedLeverage when parameter <= 0:
                throw new DomainException(DomainErrors.CopyLeverageInvalid);
        }
    }
}

public readonly record struct AccountSnapshot(double Balance, double Equity, double FreeMargin);

public readonly record struct SymbolSpec(double ContractSize, double LotStep, double MinLot, double MaxLot);

public readonly record struct CopySizingInput(
    double MasterVolumeLots,
    AccountSnapshot Master,
    AccountSnapshot Destination,
    SymbolSpec MasterSymbol,
    SymbolSpec DestinationSymbol,
    RiskSettings Risk,
    LotBounds Bounds);

public readonly record struct CopyVolume(double Lots, bool Skipped)
{
    public static CopyVolume Skip => new(0, true);
}
