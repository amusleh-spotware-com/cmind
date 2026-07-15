using Core.Constants;
using FluentAssertions;
using Xunit;

namespace UnitTests.Time;

// Gate for the time-zone picker's option source. The switcher (SettingsDialog → Time zone) offers every entry
// of SupportedTimeZones.All; a regression that collapses this to a single zone — the repo-wide
// InvariantGlobalization disabling ICU, or a missing tzdata package in a container — would leave users unable
// to pick their zone. This fails the build if the list isn't a real, full zone database. (UnitTests runs
// non-invariant so the ICU zone DB is present, mirroring the Web host.)
public class SupportedTimeZonesTests
{
    [Fact]
    public void All_offers_the_full_zone_database_not_a_single_zone()
    {
        SupportedTimeZones.All.Count.Should().BeGreaterThan(50,
            "the picker must list every real IANA zone (100+), not just UTC — a tiny count means ICU/tzdata "
            + "is missing and users cannot change their time zone");
    }

    [Theory]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Tokyo")]
    [InlineData("Australia/Sydney")]
    [InlineData("UTC")]
    public void All_contains_the_common_zones_a_user_expects(string id)
    {
        SupportedTimeZones.All.Select(o => o.Id).Should().Contain(id);
    }

    [Fact]
    public void All_has_no_duplicate_ids_and_every_option_has_a_display_name()
    {
        var all = SupportedTimeZones.All;
        all.Select(o => o.Id).Should().OnlyHaveUniqueItems();
        all.Should().OnlyContain(o => !string.IsNullOrWhiteSpace(o.Id) && !string.IsNullOrWhiteSpace(o.DisplayName));
    }
}
