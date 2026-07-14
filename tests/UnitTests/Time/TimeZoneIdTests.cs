using Core.Domain;
using Core.Time;
using FluentAssertions;
using Xunit;

namespace UnitTests.Time;

public class TimeZoneIdTests
{
    [Theory]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Tokyo")]
    [InlineData("UTC")]
    public void From_accepts_a_known_iana_zone_and_round_trips(string id)
    {
        TimeZoneId.From(id).Value.Should().Be(id);
    }

    [Fact]
    public void From_trims_whitespace()
    {
        TimeZoneId.From("  Europe/Paris  ").Value.Should().Be("Europe/Paris");
    }

    [Fact]
    public void From_normalizes_a_windows_id_to_its_canonical_iana_id()
    {
        // .NET resolves Windows ids too; the value object stores the portable IANA form.
        var zone = TimeZoneId.From("GMT Standard Time");
        zone.Value.Should().Be("Europe/London");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Not/AZone")]
    [InlineData("Mars/Olympus_Mons")]
    public void TryFrom_returns_false_for_blank_or_unknown(string? value)
    {
        TimeZoneId.TryFrom(value, out _).Should().BeFalse();
    }

    [Fact]
    public void From_throws_a_domain_exception_for_an_unknown_zone()
    {
        var act = () => TimeZoneId.From("Not/AZone");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ToTimeZoneInfo_resolves_and_converts_utc_to_wall_clock()
    {
        var zone = TimeZoneId.From("America/New_York").ToTimeZoneInfo();
        // 2026-01-15 12:00Z is 07:00 in New York (EST, UTC-5).
        var utc = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var local = TimeZoneInfo.ConvertTime(utc, zone);
        local.Hour.Should().Be(7);
    }

    [Fact]
    public void Utc_is_the_utc_zone()
    {
        TimeZoneId.Utc.Value.Should().Be("UTC");
    }
}
