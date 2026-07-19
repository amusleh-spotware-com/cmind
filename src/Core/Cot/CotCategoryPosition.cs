namespace Core.Cot;

/// <summary>
/// One trader category's positioning inside a <see cref="CotReport"/> — the long/short/spread contract counts
/// and (when published) the number of traders on each side. Owned by the report aggregate; never mutated
/// after creation, since a COT report is a fixed weekly snapshot.
/// </summary>
public sealed class CotCategoryPosition
{
    public CotTraderCategory Category { get; private set; }
    public long Long { get; private set; }
    public long Short { get; private set; }
    public long Spread { get; private set; }
    public int? TradersLong { get; private set; }
    public int? TradersShort { get; private set; }

    public long Net => Long - Short;

    private CotCategoryPosition()
    {
    }

    internal static CotCategoryPosition Create(
        CotTraderCategory category, CotPositions positions, int? tradersLong, int? tradersShort)
        => new()
        {
            Category = category,
            Long = positions.Long,
            Short = positions.Short,
            Spread = positions.Spread,
            TradersLong = tradersLong,
            TradersShort = tradersShort
        };

    public CotPositions Positions => new(Long, Short, Spread);
}
