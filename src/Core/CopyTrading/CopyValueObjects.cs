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
    AutoProportional = 8,
    RiskFromStopLoss = 9
}

public enum CopyDirectionFilter
{
    Both = 0,
    LongOnly = 1,
    ShortOnly = 2
}

// Account-level protection modes (ZuluGuard / Duplikium Global Account Protection). On breach of the
// equity threshold the destination is latched against new opens; CloseOnly and Frozen both stop opening
// new copies (existing stay managed — a full freeze of existing management is a reserved refinement),
// while SellOut additionally closes every copy immediately. Off = no guard.
public enum AccountProtectionMode
{
    Off = 0,
    CloseOnly = 1,
    Frozen = 2,
    SellOut = 3
}

public enum SymbolFilterMode
{
    None = 0,
    Whitelist = 1,
    Blacklist = 2
}

[Flags]
public enum CopyOrderTypes
{
    None = 0,
    Market = 1,
    MarketRange = 2,
    Limit = 4,
    Stop = 8,
    StopLimit = 16,
    All = Market | MarketRange | Limit | Stop | StopLimit
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

// C14: an absolute sanity ceiling on a computed copy size, defending against the catastrophic-oversize
// class (a master 0.23 lots turning into 3 lots on each receiver through a runaway multiplier / rounding
// bug). A copy is blocked when it exceeds the absolute cap, or exceeds MasterMultiple× the master's own
// size. 0 on either dimension disables that check; both 0 = disabled.
public readonly record struct LotSanityCeiling
{
    public double AbsoluteMaxLots { get; }
    public double MasterMultiple { get; }

    public LotSanityCeiling(double absoluteMaxLots, double masterMultiple)
    {
        if (absoluteMaxLots < 0 || masterMultiple < 0
            || double.IsNaN(absoluteMaxLots) || double.IsNaN(masterMultiple)
            || double.IsInfinity(absoluteMaxLots) || double.IsInfinity(masterMultiple))
            throw new DomainException(DomainErrors.CopyLotSanityInvalid);
        AbsoluteMaxLots = absoluteMaxLots;
        MasterMultiple = masterMultiple;
    }

    public static LotSanityCeiling Disabled => new(0, 0);

    public bool IsBreached(double copyLots, double masterLots)
        => (AbsoluteMaxLots > 0 && copyLots > AbsoluteMaxLots)
           || (MasterMultiple > 0 && masterLots > 0 && copyLots > MasterMultiple * masterLots);
}

// Account-level protection policy: a destination's live equity is watched, and on breach the mode is
// applied. StopEquity is the floor (equity falling to/below it triggers); TakeEquity (optional) is the
// ceiling (equity rising to/above it triggers a take-profit protection). SellOut requires a StopEquity.
public readonly record struct AccountProtectionPolicy
{
    public AccountProtectionMode Mode { get; }
    public double StopEquity { get; }
    public double? TakeEquity { get; }

    public AccountProtectionPolicy(AccountProtectionMode mode, double stopEquity, double? takeEquity)
    {
        if (stopEquity < 0 || double.IsNaN(stopEquity))
            throw new DomainException(DomainErrors.CopyAccountProtectionInvalid);
        if (takeEquity is { } take && (take <= 0 || double.IsNaN(take) || take <= stopEquity))
            throw new DomainException(DomainErrors.CopyAccountProtectionInvalid);
        if (mode == AccountProtectionMode.SellOut && stopEquity <= 0)
            throw new DomainException(DomainErrors.CopyAccountProtectionInvalid);
        Mode = mode;
        StopEquity = stopEquity;
        TakeEquity = takeEquity;
    }

    public static AccountProtectionPolicy Off => new(AccountProtectionMode.Off, 0, null);

    public bool IsTriggered(double equity)
        => Mode != AccountProtectionMode.Off
           && ((StopEquity > 0 && equity <= StopEquity) || (TakeEquity is { } take && equity >= take));
}

// C18: a per-destination daily trading-hours window (UTC minutes-of-day). New opens outside the window
// are skipped. Start == End means "all day" (disabled). A window whose Start > End wraps past midnight
// (e.g. 22:00–06:00). End is exclusive.
public readonly record struct TradingWindow
{
    public int StartMinuteUtc { get; }
    public int EndMinuteUtc { get; }

    public TradingWindow(int startMinuteUtc, int endMinuteUtc)
    {
        if (startMinuteUtc is < 0 or > 1439 || endMinuteUtc is < 0 or > 1439)
            throw new DomainException(DomainErrors.CopyTradingWindowInvalid);
        StartMinuteUtc = startMinuteUtc;
        EndMinuteUtc = endMinuteUtc;
    }

    public static TradingWindow AllDay => new(0, 0);
    public bool IsAllDay => StartMinuteUtc == EndMinuteUtc;

    public bool IsOpenAt(int minuteOfDayUtc)
    {
        if (IsAllDay) return true;
        return StartMinuteUtc <= EndMinuteUtc
            ? minuteOfDayUtc >= StartMinuteUtc && minuteOfDayUtc < EndMinuteUtc
            : minuteOfDayUtc >= StartMinuteUtc || minuteOfDayUtc < EndMinuteUtc;
    }
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

    public static RiskSettings Default => new(MoneyManagementMode.LotMultiplier, 1);

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
            case MoneyManagementMode.FixedRiskPercent or MoneyManagementMode.RiskFromStopLoss
                when parameter is <= 0 or > 100:
                throw new DomainException(DomainErrors.CopyRiskParameterInvalid);
            case MoneyManagementMode.FixedLeverage when parameter <= 0:
                throw new DomainException(DomainErrors.CopyLeverageInvalid);
        }
    }
}

public readonly record struct NodeIdentity
{
    public string Value { get; }

    public NodeIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new DomainException(DomainErrors.CopyNodeIdentityInvalid);
        Value = value.Trim();
    }

    public override string ToString() => Value;
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
    LotBounds Bounds,
    double MasterStopDistance = 0);

public readonly record struct CopyVolume(double Lots, bool Skipped)
{
    public static CopyVolume Skip => new(0, true);
}
