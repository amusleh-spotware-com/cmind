using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core;

// ---------------- Copy trading: profile (source -> destinations) ----------------

public sealed class CopySymbolMapEntry
{
    public string Source { get; private set; } = default!;
    public string Destination { get; private set; } = default!;

    private CopySymbolMapEntry()
    {
    }

    public CopySymbolMapEntry(Symbol source, Symbol destination)
    {
        Source = source.Value;
        Destination = destination.Value;
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
    public bool CopyPendingOrders { get; private set; }
    public bool CopyTrailingStop { get; private set; }
    public CopyOrderTypes CopyOrderTypes { get; private set; } = CopyOrderTypes.All;
    public bool CopyPendingExpiry { get; private set; } = true;
    public bool CopyMasterSlippage { get; private set; } = true;
    public CopyDirectionFilter Direction { get; private set; } = CopyDirectionFilter.Both;
    public double MinLot { get; private set; }
    public double MaxLot { get; private set; }
    public bool ForceMinLot { get; private set; }
    public double MaxDrawdownPercent { get; private set; }
    public double DailyLossLimit { get; private set; }
    public double LotSanityAbsoluteMaxLots { get; private set; }
    public double LotSanityMasterMultiple { get; private set; }
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
            _symbolMaps.Add(new CopySymbolMapEntry(entry.Source, entry.Destination));
    }

    public string ResolveDestinationSymbol(string sourceSymbol)
        => _symbolMaps.FirstOrDefault(m => string.Equals(m.Source, sourceSymbol, StringComparison.OrdinalIgnoreCase))
            ?.Destination ?? sourceSymbol;

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

    public void RemoveDestination(CopyDestinationId destinationId)
    {
        var destination = _destinations.FirstOrDefault(d => d.Id == destinationId);
        if (destination is null) return;
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
