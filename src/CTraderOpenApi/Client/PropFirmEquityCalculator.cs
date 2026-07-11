namespace CTraderOpenApi.Client;

/// <summary>
/// A single open position's valuation inputs. <paramref name="Volume"/> is the cTrader wire volume
/// (units × 100); <paramref name="EntryPrice"/>, <paramref name="Swap"/> and <paramref name="Commission"/>
/// are already scaled to real values (price in quote currency, money amounts in the deposit currency).
/// </summary>
public sealed record PositionValuation(
    long PositionId,
    long SymbolId,
    bool IsBuy,
    long Volume,
    double EntryPrice,
    double Swap,
    double Commission);

/// <summary>
/// Live pricing for a symbol. <paramref name="QuoteToDepositRate"/> converts a P&amp;L expressed in the
/// symbol's quote currency into the account deposit currency (1.0 when quote == deposit).
/// </summary>
public sealed record SymbolPricing(long SymbolId, double Bid, double Ask, double QuoteToDepositRate = 1.0);

/// <summary>The result of revaluing an account: equity and the floating (unrealized) P&amp;L component.</summary>
public readonly record struct EquityResult(double Equity, double FloatingPnL, double Balance);

/// <summary>
/// Computes account equity the way the cTrader Open API requires: equity is not delivered by the API, so it
/// is derived as <c>balance + Σ(unrealized P&amp;L)</c>. Unrealized P&amp;L per position is
/// <c>priceDifference × units × quote→deposit rate + swap + commission</c>, where <c>units = volume / 100</c>
/// (the wire volume is scaled by 100). A long revalues at the bid, a short at the ask. Pure and deterministic
/// — the currency-conversion hot spot lives here and is unit-tested in isolation.
/// </summary>
public sealed class PropFirmEquityCalculator
{
    private const double VolumeToUnits = 100.0;

    public EquityResult Compute(
        double balance,
        IReadOnlyCollection<PositionValuation> positions,
        IReadOnlyDictionary<long, SymbolPricing> pricing)
    {
        var floating = 0.0;
        foreach (var position in positions)
        {
            if (!pricing.TryGetValue(position.SymbolId, out var price)) continue;

            var units = position.Volume / VolumeToUnits;
            var priceDifference = position.IsBuy
                ? price.Bid - position.EntryPrice
                : position.EntryPrice - price.Ask;

            floating += priceDifference * units * price.QuoteToDepositRate
                        + position.Swap + position.Commission;
        }

        return new EquityResult(balance + floating, floating, balance);
    }
}
