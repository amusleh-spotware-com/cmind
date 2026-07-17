using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core;

// ---------------- Copy trading: profile (source -> destinations) ----------------

public sealed class CopySymbolMapEntry
{
    public string Source { get; private set; } = default!;
    public string Destination { get; private set; } = default!;
    public double VolumeMultiplier { get; private set; } = 1;

    private CopySymbolMapEntry()
    {
    }

    public CopySymbolMapEntry(Symbol source, Symbol destination, double volumeMultiplier = 1)
    {
        Source = source.Value;
        Destination = destination.Value;
        VolumeMultiplier = volumeMultiplier;
    }
}

public sealed class CopySymbolFilter
{
    public string Symbol { get; private set; } = default!;

    private CopySymbolFilter()
    {
    }

    public CopySymbolFilter(Symbol symbol) => Symbol = symbol.Value;
}

public class CopyDestination : AuditedEntity<CopyDestinationId>
{
    private readonly List<CopySymbolMapEntry> _symbolMaps = [];
    private readonly List<CopySymbolFilter> _symbolFilters = [];

    public CopyProfileId ProfileId { get; private set; }
    public TradingAccountId DestinationAccountId { get; private set; }
    public MoneyManagementMode RiskMode { get; private set; }
    public double RiskParameter { get; private set; }
    public double SlippagePips { get; private set; }
    public int MaxDelaySeconds { get; private set; }
    public bool Reverse { get; private set; }
    public bool CopyStopLoss { get; private set; } = true;
    public bool CopyTakeProfit { get; private set; } = true;
    public bool MirrorPartialClose { get; private set; } = true;
    public bool MirrorScaleIn { get; private set; }
    public bool CopyPendingOrders { get; private set; } = true;
    public bool CopyTrailingStop { get; private set; }
    public CopyOrderTypes CopyOrderTypes { get; private set; } = CopyOrderTypes.All;
    public bool CopyPendingExpiry { get; private set; } = true;
    public bool CopyMasterSlippage { get; private set; } = true;
    // Manage-only (Ignore-New-Trades / Duplikium "Close-Only"): mirror closes, partial closes and
    // protection changes on positions already copied, but open no new positions or pendings.
    public bool ManageOnly { get; private set; }
    // Sync-on-start policy (cMAM). SyncOpenOnStart: on the profile's first resync, open copies for the
    // master's pre-existing positions. SyncClosedOnStart: on first resync, close copies the master closed
    // while the profile was stopped. Both apply only to the initial resync — a mid-run reconnect always
    // reconciles fully so a desync recovers.
    public bool SyncOpenOnStart { get; private set; } = true;
    public bool SyncClosedOnStart { get; private set; } = true;
    // Source-label filter (cTrader equivalent of an MT magic-number filter): when set, copy only master
    // trades whose label matches exactly — e.g. copy one bot's trades, or manual-only. Empty = copy all.
    [MaxLength(128)] public string? SourceLabelFilter { get; private set; }
    public CopyDirectionFilter Direction { get; private set; } = CopyDirectionFilter.Both;
    public double MinLot { get; private set; }
    public double MaxLot { get; private set; }
    public bool ForceMinLot { get; private set; }
    public double MaxDrawdownPercent { get; private set; }
    public double DailyLossLimit { get; private set; }
    // M7 max-risk fallback: for RiskFromStopLoss sizing, the fixed lot to use when the master has no
    // stop-loss (so there's no distance to derive risk from). 0 = skip unstopped masters (no_stop_loss).
    public double RiskFallbackLots { get; private set; }
    public double LotSanityAbsoluteMaxLots { get; private set; }
    public double LotSanityMasterMultiple { get; private set; }
    public int TradingHoursStartMinuteUtc { get; private set; }
    public int TradingHoursEndMinuteUtc { get; private set; }
    public AccountProtectionMode AccountProtectionMode { get; private set; } = AccountProtectionMode.Off;
    public double AccountProtectionStopEquity { get; private set; }
    public double? AccountProtectionTakeEquity { get; private set; }
    public double PropRuleDailyLossCap { get; private set; }
    public double PropRuleTrailingDrawdown { get; private set; }
    // Consistency pre-alert (C10): warn when a destination's daily profit reaches this percent of the day's
    // opening equity, so a prop-firm consistency rule can be respected before it trips. 0 = off.
    public double ConsistencyThresholdPercent { get; private set; }
    // Config lock (C9): while set and in the future, the destination's settings are frozen against edits/
    // removal — a deliberate guard against impulsive changes during a drawdown.
    public DateTimeOffset? ConfigLockedUntil { get; private set; }
    // Execution jitter (C11): a random 0..N ms delay before placing a copy, to de-correlate otherwise
    // microsecond-identical order timestamps across the user's own accounts. 0 = off. A compliance aid for
    // firms that PERMIT copying — never a tool to evade a firm that forbids it (the user's responsibility).
    public int ExecutionJitterMaxMs { get; private set; }
    // Phase 4 performance fee (high-water-mark model, as cTrader Copy / Darwinex / ZuluTrade profit-share):
    // the provider charges this percent of NEW profit above the follower's peak equity only. 0 = no fee.
    public double PerformanceFeePercent { get; private set; }
    // The follower's highest settled equity. Fees accrue only on gains above it and it never decreases, so a
    // follower recovering a drawdown is never charged twice for ground already settled.
    public double HighWaterMarkEquity { get; private set; }
    public SymbolFilterMode SymbolFilterMode { get; private set; } = SymbolFilterMode.None;
    public IReadOnlyList<CopySymbolMapEntry> SymbolMaps => _symbolMaps;
    public IReadOnlyList<CopySymbolFilter> SymbolFilters => _symbolFilters;

    private CopyDestination()
    {
    }

    public static CopyDestination Create(CopyProfileId profileId, TradingAccountId destinationAccountId,
        RiskSettings risk)
        => new()
        {
            ProfileId = profileId,
            DestinationAccountId = destinationAccountId,
            RiskMode = risk.Mode,
            RiskParameter = risk.Parameter
        };

    public RiskSettings Risk => new(RiskMode, RiskParameter);
    public LotBounds Bounds => new(MinLot, MaxLot, ForceMinLot);
    public LotSanityCeiling LotSanity => new(LotSanityAbsoluteMaxLots, LotSanityMasterMultiple);
    public TradingWindow TradingHours => new(TradingHoursStartMinuteUtc, TradingHoursEndMinuteUtc);
    public AccountProtectionPolicy AccountProtection =>
        new(AccountProtectionMode, AccountProtectionStopEquity, AccountProtectionTakeEquity);
    public PropRuleGuard PropRules => new(PropRuleDailyLossCap, PropRuleTrailingDrawdown);

    public void ConfigureRisk(RiskSettings risk)
    {
        RiskMode = risk.Mode;
        RiskParameter = risk.Parameter;
    }

    public void ConfigureBounds(LotBounds bounds)
    {
        MinLot = bounds.MinLot;
        MaxLot = bounds.MaxLot;
        ForceMinLot = bounds.ForceMinLot;
    }

    public void ConfigureLotSanity(LotSanityCeiling ceiling)
    {
        LotSanityAbsoluteMaxLots = ceiling.AbsoluteMaxLots;
        LotSanityMasterMultiple = ceiling.MasterMultiple;
    }

    public void ConfigureTradingHours(TradingWindow window)
    {
        TradingHoursStartMinuteUtc = window.StartMinuteUtc;
        TradingHoursEndMinuteUtc = window.EndMinuteUtc;
    }

    public void SetAccountProtection(AccountProtectionPolicy policy)
    {
        AccountProtectionMode = policy.Mode;
        AccountProtectionStopEquity = policy.StopEquity;
        AccountProtectionTakeEquity = policy.TakeEquity;
    }

    public void SetPropRuleGuard(PropRuleGuard guard)
    {
        PropRuleDailyLossCap = guard.DailyLossCap;
        PropRuleTrailingDrawdown = guard.TrailingDrawdown;
    }

    public void SetConsistencyThreshold(double percent)
    {
        DomainGuard.AgainstNegative(percent, DomainErrors.CopyRiskParameterInvalid);
        ConsistencyThresholdPercent = percent;
    }

    public void LockConfig(DateTimeOffset until)
    {
        ConfigLockedUntil = until;
    }

    public void SetExecutionJitter(int maxMilliseconds)
    {
        DomainGuard.AgainstNegative(maxMilliseconds, DomainErrors.CopyRiskParameterInvalid);
        ExecutionJitterMaxMs = maxMilliseconds;
    }

    public void SetPerformanceFee(PerformanceFee fee) => PerformanceFeePercent = fee.Percent;

    // High-water-mark settlement (the standard copy-trading performance-fee model): returns the fee accrued
    // for this period and advances the HWM. The first settlement seeds the HWM at the current equity, so the
    // follower is never charged on their opening balance; thereafter a fee is charged only on equity ABOVE
    // the previous peak, and at or below the peak nothing is charged and the peak is unchanged (the follower
    // must first recover past it). Fee logic lives on the aggregate — the settlement service only supplies
    // the polled equity and records the returned amount.
    public double SettleFee(double equity)
    {
        if (PerformanceFeePercent <= 0) return 0;
        if (HighWaterMarkEquity <= 0)
        {
            HighWaterMarkEquity = equity > 0 ? equity : 0;
            return 0;
        }
        if (equity <= HighWaterMarkEquity) return 0;
        var fee = (equity - HighWaterMarkEquity) * PerformanceFeePercent / 100.0;
        HighWaterMarkEquity = equity;
        return fee;
    }

    public bool IsConfigLocked(DateTimeOffset now) => ConfigLockedUntil is { } until && until > now;

    public void ConfigureSlippage(SlippagePips slippage)
    {
        SlippagePips = slippage.Value;
    }

    public void ConfigureMaxDelay(MaxCopyDelay delay)
    {
        MaxDelaySeconds = (int) delay.Value.TotalSeconds;
    }

    public void SetReverse(bool reverse)
    {
        Reverse = reverse;
    }

    public void SetCopyProtection(bool copyStopLoss, bool copyTakeProfit)
    {
        CopyStopLoss = copyStopLoss;
        CopyTakeProfit = copyTakeProfit;
    }

    public void SetDirection(CopyDirectionFilter direction)
    {
        Direction = direction;
    }

    public void SetPartialCloseMirroring(bool mirrorPartialClose, bool mirrorScaleIn)
    {
        MirrorPartialClose = mirrorPartialClose;
        MirrorScaleIn = mirrorScaleIn;
    }

    public void SetPendingOrderCopying(bool copyPendingOrders)
    {
        CopyPendingOrders = copyPendingOrders;
    }

    public void SetTrailingStopCopying(bool copyTrailingStop)
    {
        CopyTrailingStop = copyTrailingStop;
    }

    public void SetOrderTypeFilter(CopyOrderTypes orderTypes)
    {
        if (orderTypes == CopyOrderTypes.None)
            throw new DomainException(DomainErrors.CopyOrderTypesInvalid);
        CopyOrderTypes = orderTypes;
    }

    public void SetExpiryCopying(bool copyPendingExpiry)
    {
        CopyPendingExpiry = copyPendingExpiry;
    }

    public void SetSlippageCopying(bool copyMasterSlippage)
    {
        CopyMasterSlippage = copyMasterSlippage;
    }

    public void SetManageOnly(bool manageOnly)
    {
        ManageOnly = manageOnly;
    }

    public void SetRiskFallbackLots(double lots)
    {
        DomainGuard.AgainstNegative(lots, DomainErrors.CopyLotInvalid);
        RiskFallbackLots = lots;
    }

    public void SetSyncPolicy(bool syncOpenOnStart, bool syncClosedOnStart)
    {
        SyncOpenOnStart = syncOpenOnStart;
        SyncClosedOnStart = syncClosedOnStart;
    }

    public void SetSourceLabelFilter(string? sourceLabel)
    {
        SourceLabelFilter = string.IsNullOrWhiteSpace(sourceLabel) ? null : sourceLabel.Trim();
    }

    public bool IsSourceLabelAllowed(string? sourceLabel)
        => SourceLabelFilter is null || string.Equals(SourceLabelFilter, sourceLabel, StringComparison.Ordinal);

    public bool IsOrderTypeAllowed(CopyOrderTypes orderType)
        => orderType != CopyOrderTypes.None && (CopyOrderTypes & orderType) == orderType;

    public void SetGuards(DrawdownPercent maxDrawdown, double dailyLossLimit)
    {
        DomainGuard.AgainstNegative(dailyLossLimit, DomainErrors.DrawdownOutOfRange);
        MaxDrawdownPercent = maxDrawdown.Value;
        DailyLossLimit = dailyLossLimit;
    }

    public void SetSymbolMap(IEnumerable<SymbolMapEntry> entries)
    {
        _symbolMaps.Clear();
        foreach (var entry in entries)
            _symbolMaps.Add(new CopySymbolMapEntry(entry.Source, entry.Destination, entry.VolumeMultiplier));
    }

    public string ResolveDestinationSymbol(string sourceSymbol)
        => _symbolMaps.FirstOrDefault(m => string.Equals(m.Source, sourceSymbol, StringComparison.OrdinalIgnoreCase))
            ?.Destination ?? sourceSymbol;

    // Per-symbol volume multiplier for a source symbol (1 when unmapped or unset).
    public double ResolveVolumeMultiplier(string sourceSymbol)
        => _symbolMaps.FirstOrDefault(m => string.Equals(m.Source, sourceSymbol, StringComparison.OrdinalIgnoreCase))
            ?.VolumeMultiplier ?? 1;

    public void SetSymbolFilter(SymbolFilterMode mode, IEnumerable<Symbol> symbols)
    {
        SymbolFilterMode = mode;
        _symbolFilters.Clear();
        foreach (var symbol in symbols)
            _symbolFilters.Add(new CopySymbolFilter(symbol));
    }

    public bool IsSymbolAllowed(string sourceSymbol) => SymbolFilterMode switch
    {
        SymbolFilterMode.Whitelist => _symbolFilters.Any(f =>
            string.Equals(f.Symbol, sourceSymbol, StringComparison.OrdinalIgnoreCase)),
        SymbolFilterMode.Blacklist => !_symbolFilters.Any(f =>
            string.Equals(f.Symbol, sourceSymbol, StringComparison.OrdinalIgnoreCase)),
        _ => true
    };
}

public class CopyProfile : AuditedEntity<CopyProfileId>
{
    private readonly List<CopyDestination> _destinations = [];

    public UserId UserId { get; private set; }
    [MaxLength(128)] public string Name { get; private set; } = default!;
    public TradingAccountId SourceAccountId { get; private set; }
    public CopyProfileStatus Status { get; private set; } = CopyProfileStatus.Draft;
    [MaxLength(64)] public string? AssignedNode { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    // Flatten-all panic request (C8): set by the user via the API; the supervisor routes it to the running
    // host (which closes + locks every destination), then clears it.
    public DateTimeOffset? FlattenRequestedAt { get; private set; }
    public IReadOnlyList<CopyDestination> Destinations => _destinations;

    public static CopyProfile Create(UserId userId, string name, TradingAccountId sourceAccountId)
        => new()
        {
            UserId = userId,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired),
            SourceAccountId = sourceAccountId,
            Status = CopyProfileStatus.Draft
        };

    public void Rename(string name)
    {
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.NameRequired);
    }

    // Change the source (master) account. The new source must not already be one of this profile's
    // destinations — a source can never copy from itself (same invariant AddDestination guards).
    public void ChangeSource(TradingAccountId sourceAccountId)
    {
        if (_destinations.Any(d => d.DestinationAccountId == sourceAccountId))
            throw new DomainException(DomainErrors.CopySourceEqualsDestination);
        SourceAccountId = sourceAccountId;
    }

    public bool IsHostedBy(NodeIdentity node) => string.Equals(AssignedNode, node.Value, StringComparison.Ordinal);

    public void AssignToNode(NodeIdentity node)
    {
        AssignedNode = node.Value;
    }

    // A node claims a running profile for a bounded lease, renews it while alive, and — if it dies —
    // the lease lapses so any other node can reclaim the profile. This is what makes copy hosting
    // self-heal across a horizontally scaled cluster.
    public void ClaimBy(NodeIdentity node, DateTimeOffset leaseUntil)
    {
        AssignedNode = node.Value;
        LeaseExpiresAt = leaseUntil;
    }

    public void RenewLease(DateTimeOffset leaseUntil)
    {
        LeaseExpiresAt = leaseUntil;
    }

    public bool IsLeaseHeldBy(NodeIdentity node, DateTimeOffset now)
        => string.Equals(AssignedNode, node.Value, StringComparison.Ordinal) && LeaseExpiresAt > now;

    public void ReleaseAssignment()
    {
        AssignedNode = null;
        LeaseExpiresAt = null;
    }

    public void RequestFlatten(DateTimeOffset now)
    {
        FlattenRequestedAt = now;
    }

    public void ClearFlattenRequest()
    {
        FlattenRequestedAt = null;
    }

    public CopyDestination AddDestination(TradingAccountId destinationAccountId, RiskSettings risk)
    {
        if (destinationAccountId == SourceAccountId)
            throw new DomainException(DomainErrors.CopySourceEqualsDestination);
        if (_destinations.Any(d => d.DestinationAccountId == destinationAccountId))
            throw new DomainException(DomainErrors.CopyDestinationDuplicate);

        var destination = CopyDestination.Create(Id, destinationAccountId, risk);
        _destinations.Add(destination);

        return destination;
    }

    public void RemoveDestination(CopyDestinationId destinationId, DateTimeOffset now)
    {
        var destination = _destinations.FirstOrDefault(d => d.Id == destinationId);
        if (destination is null) return;
        if (destination.IsConfigLocked(now))
            throw new DomainException(DomainErrors.CopyDestinationConfigLocked);
        _destinations.Remove(destination);
    }

    public void Start()
    {
        if (Status is not (CopyProfileStatus.Draft or CopyProfileStatus.Paused or CopyProfileStatus.Stopped))
            throw new DomainException(DomainErrors.CopyProfileTransitionInvalid);
        Status = CopyProfileStatus.Running;

        RaiseDomainEvent(new CopyProfileStarted(Id, UserId));
    }

    public void Pause()
    {
        if (Status != CopyProfileStatus.Running)
            throw new DomainException(DomainErrors.CopyProfileTransitionInvalid);
        Status = CopyProfileStatus.Paused;
        AssignedNode = null;
        LeaseExpiresAt = null;

        RaiseDomainEvent(new CopyProfilePaused(Id, UserId));
    }

    public void Stop()
    {
        Status = CopyProfileStatus.Stopped;
        AssignedNode = null;
        LeaseExpiresAt = null;

        RaiseDomainEvent(new CopyProfileStopped(Id, UserId));
    }

    public void MarkError(string reason)
    {
        Status = CopyProfileStatus.Error;

        RaiseDomainEvent(new CopyProfileErrored(Id, UserId, reason));
    }
}
