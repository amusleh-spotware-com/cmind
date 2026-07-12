using System.Globalization;
using Core.Constants;
using Core.Domain;

namespace Core.Execution;

public enum OrderSide
{
    Buy,
    Sell
}

/// <summary>A single execution against an order: the price and quantity that filled.</summary>
public sealed record Fill(double Price, double Quantity);

/// <summary>
/// Execution-quality metrics for an order: how far the achieved price drifted from the decision
/// (arrival) price. Slippage is expressed in basis points and signed so that a positive number is a
/// cost to the trader; implementation shortfall is that cost in price×quantity terms. Deterministic.
/// </summary>
public sealed record TransactionCostAnalysis(
    double ArrivalPrice,
    double AverageFillPrice,
    double FilledQuantity,
    OrderSide Side,
    double SlippageBps,
    double ImplementationShortfall,
    string Rationale);

public interface ITransactionCostAnalyzer
{
    TransactionCostAnalysis Analyze(double arrivalPrice, OrderSide side, IReadOnlyList<Fill> fills);
}

public sealed class TransactionCostAnalyzer : ITransactionCostAnalyzer
{
    private const double BasisPoints = 10_000.0;

    public TransactionCostAnalysis Analyze(double arrivalPrice, OrderSide side, IReadOnlyList<Fill> fills)
    {
        ArgumentNullException.ThrowIfNull(fills);
        if (!(arrivalPrice > 0.0) || double.IsNaN(arrivalPrice)) throw new DomainException(DomainErrors.ExecutionInputInvalid);
        if (fills.Count == 0) throw new DomainException(DomainErrors.ExecutionInputInvalid);

        double notional = 0, quantity = 0;
        foreach (var f in fills)
        {
            if (!(f.Price > 0.0) || !(f.Quantity > 0.0) || double.IsNaN(f.Price) || double.IsNaN(f.Quantity))
                throw new DomainException(DomainErrors.ExecutionInputInvalid);
            notional += f.Price * f.Quantity;
            quantity += f.Quantity;
        }

        var vwap = notional / quantity;

        // Cost sign convention: buying above arrival, or selling below it, is a positive cost.
        var priceCost = side == OrderSide.Buy ? vwap - arrivalPrice : arrivalPrice - vwap;
        var slippageBps = priceCost / arrivalPrice * BasisPoints;
        var shortfall = priceCost * quantity;

        return new TransactionCostAnalysis(arrivalPrice, vwap, quantity, side, slippageBps, shortfall,
            string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1:0.####} filled at VWAP {2:0.#####} vs arrival {3:0.#####}: slippage {4:0.0} bps ({5} to you), implementation shortfall {6:0.#####}.",
                side, quantity, vwap, arrivalPrice, slippageBps, slippageBps >= 0 ? "a cost" : "a gain", shortfall));
    }
}
