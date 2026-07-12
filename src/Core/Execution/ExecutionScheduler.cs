using Core.Constants;
using Core.Domain;

namespace Core.Execution;

/// <summary>One tranche of an execution schedule: which slice and how much to trade in it.</summary>
public sealed record ExecutionSlice(int Index, double Quantity);

public interface IExecutionScheduler
{
    /// <summary>
    /// Builds an optimal execution trajectory (Almgren-Chriss). Higher risk aversion front-loads the
    /// schedule to cut timing risk; zero risk aversion degenerates to an even TWAP.
    /// </summary>
    IReadOnlyList<ExecutionSlice> Schedule(
        double totalQuantity, int slices, double riskAversion, double volatility, double temporaryImpact);
}

/// <summary>
/// Closed-form Almgren-Chriss optimal-execution schedule. With trading-rate urgency
/// κ = √(λσ²/η), the remaining holdings follow x_j = X·sinh(κ(1−j/N)) / sinh(κ); slice sizes are the
/// successive differences. Deterministic; the slices always sum to the total quantity.
/// </summary>
public sealed class AlmgrenChrissScheduler : IExecutionScheduler
{
    public IReadOnlyList<ExecutionSlice> Schedule(
        double totalQuantity, int slices, double riskAversion, double volatility, double temporaryImpact)
    {
        if (!(totalQuantity > 0) || double.IsNaN(totalQuantity)) throw new DomainException(DomainErrors.ExecutionInputInvalid);
        if (slices < 1) throw new DomainException(DomainErrors.ExecutionInputInvalid);
        if (riskAversion < 0 || volatility < 0 || temporaryImpact < 0) throw new DomainException(DomainErrors.ExecutionInputInvalid);

        var kappa = temporaryImpact > 0 && riskAversion > 0
            ? Math.Sqrt(riskAversion * volatility * volatility / temporaryImpact)
            : 0.0;

        var result = new List<ExecutionSlice>(slices);

        // Risk-neutral / no urgency → even TWAP.
        var sinhK = Math.Sinh(kappa);
        if (kappa <= 0 || sinhK <= 0)
        {
            var even = totalQuantity / slices;
            for (var j = 1; j <= slices; j++) result.Add(new ExecutionSlice(j, even));
            return result;
        }

        double Holdings(int j) => totalQuantity * Math.Sinh(kappa * (1.0 - (double)j / slices)) / sinhK;

        var previous = Holdings(0); // == totalQuantity
        for (var j = 1; j <= slices; j++)
        {
            var current = Holdings(j);
            result.Add(new ExecutionSlice(j, previous - current));
            previous = current;
        }
        return result;
    }
}
