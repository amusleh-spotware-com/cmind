using Core.Calendar;

namespace Infrastructure.Calendar;

/// <summary>A core indicator definition seeded into the catalog so the common case is warm out of the box.</summary>
public sealed record CalendarSeriesSeed(
    SeriesCode Code,
    CountryCode Country,
    string Name,
    MarketMovingCategory Category,
    ReleaseCadence Cadence,
    double ImpactPrior,
    string SourceName,
    string SourceSeriesId);

/// <summary>
/// The core high-impact US series the calendar tracks by default, mapped to their FRED series ids. The
/// proactive backfill pulls their vintage history so the calendar is populated without waiting for a user
/// miss; the long tail fills in lazily on demand. Extend as more sources land (BLS/BEA/ECB in P3+).
/// </summary>
public static class CalendarSeedData
{
    public static readonly IReadOnlyList<CalendarSeriesSeed> CoreSeries =
    [
        new(new SeriesCode("US.CPI"), new CountryCode("US"), "US CPI",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, 0.85, "FRED", "CPIAUCSL"),
        new(new SeriesCode("US.CPI.CORE"), new CountryCode("US"), "US Core CPI",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, 0.80, "FRED", "CPILFESL"),
        new(new SeriesCode("US.NFP"), new CountryCode("US"), "US Nonfarm Payrolls",
            MarketMovingCategory.Employment, ReleaseCadence.Monthly, 0.95, "FRED", "PAYEMS"),
        new(new SeriesCode("US.UNRATE"), new CountryCode("US"), "US Unemployment Rate",
            MarketMovingCategory.Employment, ReleaseCadence.Monthly, 0.75, "FRED", "UNRATE"),
        new(new SeriesCode("US.GDP"), new CountryCode("US"), "US Real GDP",
            MarketMovingCategory.Growth, ReleaseCadence.Quarterly, 0.85, "FRED", "GDPC1"),
        new(new SeriesCode("US.PCE"), new CountryCode("US"), "US PCE Price Index",
            MarketMovingCategory.Inflation, ReleaseCadence.Monthly, 0.80, "FRED", "PCEPI"),
        new(new SeriesCode("US.FEDFUNDS"), new CountryCode("US"), "US Federal Funds Rate",
            MarketMovingCategory.InterestRate, ReleaseCadence.Monthly, 0.90, "FRED", "FEDFUNDS"),
        new(new SeriesCode("US.RETAIL"), new CountryCode("US"), "US Retail Sales",
            MarketMovingCategory.Consumption, ReleaseCadence.Monthly, 0.70, "FRED", "RSAFS")
    ];
}
