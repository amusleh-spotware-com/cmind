using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.Options;

// The economic-calendar ingestion and the currency-strength refresh workers ship ON by default so both
// surfaces are warm out of the box (the earlier off-by-default left the pages permanently empty). A
// deployment or the owner turns them off — see CalendarEnablement gating + white-label/AI feature flags.
public class MarketDataWarmupDefaultsTests
{
    [Fact]
    public void Calendar_ingestion_is_enabled_by_default()
        => new CalendarOptions().IngestionEnabled.Should().BeTrue();

    [Fact]
    public void Currency_strength_refresh_is_enabled_by_default()
        => new CurrencyStrengthOptions().RefreshEnabled.Should().BeTrue();

    [Fact]
    public void Both_warmups_remain_explicitly_disablable()
    {
        new CalendarOptions { IngestionEnabled = false }.IngestionEnabled.Should().BeFalse();
        new CurrencyStrengthOptions { RefreshEnabled = false }.RefreshEnabled.Should().BeFalse();
    }
}
