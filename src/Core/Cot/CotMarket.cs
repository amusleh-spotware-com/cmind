using System.ComponentModel.DataAnnotations;
using Core.Constants;
using Core.Domain;

namespace Core.Cot;

/// <summary>
/// A tracked CFTC contract market in the catalog — "EURO FX" on the CME, "GOLD" on COMEX. Identified by its
/// stable <see cref="ContractCode"/> and optionally mapped to a tradeable <see cref="Symbol"/> so a cBot can
/// pull positioning for the instrument it trades. Concrete weekly snapshots are <see cref="CotReport"/>
/// aggregates that reference the market by strong id.
/// </summary>
public sealed class CotMarket : AuditedEntity<CotMarketId>
{
    [MaxLength(16)] public string ContractCodeValue { get; private set; } = default!;
    [MaxLength(160)] public string Name { get; private set; } = default!;
    [MaxLength(96)] public string Exchange { get; private set; } = default!;
    public CotContractGroup Group { get; private set; }

    /// <summary>The tradeable symbol this contract maps to (e.g. <c>EURUSD</c>), or null when unmapped.</summary>
    [MaxLength(32)] public string? MappedSymbolValue { get; private set; }

    public ContractMarketCode ContractCode => new(ContractCodeValue);
    public Symbol? MappedSymbol => MappedSymbolValue is null ? null : new Symbol(MappedSymbolValue);

    private CotMarket()
    {
    }

    public static CotMarket Create(
        ContractMarketCode code,
        string name,
        string exchange,
        CotContractGroup group,
        Symbol? mappedSymbol)
        => new()
        {
            ContractCodeValue = code.Value,
            Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.CotMarketNameRequired),
            Exchange = exchange?.Trim() ?? string.Empty,
            Group = group,
            MappedSymbolValue = mappedSymbol?.Value
        };

    public void Rename(string name) =>
        Name = DomainGuard.AgainstNullOrWhiteSpace(name, DomainErrors.CotMarketNameRequired);

    public void Reclassify(CotContractGroup group) => Group = group;

    public void MapSymbol(Symbol? symbol) => MappedSymbolValue = symbol?.Value;
}
