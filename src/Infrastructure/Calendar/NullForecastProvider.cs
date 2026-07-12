using Core.Calendar;

namespace Infrastructure.Calendar;

/// <summary>
/// The default forecast port: returns <c>null</c> and never fabricates a consensus. Primary sources do not
/// publish the survey median; a deployment overrides this with a licensed feed if it has one.
/// </summary>
public sealed class NullForecastProvider : IForecastProvider
{
    public Task<decimal?> GetConsensusAsync(SeriesCode series, DateTimeOffset effectiveAt, CancellationToken ct)
        => Task.FromResult<decimal?>(null);
}
