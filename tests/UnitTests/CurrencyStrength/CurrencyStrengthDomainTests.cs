using Core.Ai.CurrencyStrength;
using Core.Constants;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.CurrencyStrength;

// Invariants for the currency-strength domain primitives: the Currency value object, the CurrencyUniverse
// set rules, the config-or-default universe factory, and snapshot creation. (WS-1 Core backfill.)
public class CurrencyStrengthDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Currency_normalizes_code_and_peg_anchor()
    {
        var currency = new Currency("usd", CurrencyTier.Major);
        currency.Code.Should().Be("USD");

        var pegged = new Currency("hkd", CurrencyTier.Exotic, isPegged: true, pegAnchor: " usd ");
        pegged.IsPegged.Should().BeTrue();
        pegged.PegAnchor.Should().Be("USD");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDX")]
    [InlineData("  ")]
    public void Currency_rejects_a_non_three_letter_code(string code)
    {
        var act = () => new Currency(code, CurrencyTier.Major);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CalendarCurrencyCodeInvalid);
    }

    [Fact]
    public void Universe_rejects_empty_and_duplicate_sets()
    {
        var empty = () => CurrencyUniverse.Of([]);
        empty.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyUniverseEmpty);

        var dup = () => CurrencyUniverse.Of([new Currency("USD", CurrencyTier.Major), new Currency("USD", CurrencyTier.Major)]);
        dup.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CurrencyUniverseDuplicate);
    }

    [Fact]
    public void Factory_falls_back_to_the_curated_default_when_config_is_empty()
    {
        var universe = CurrencyUniverseFactory.FromConfig(new CurrencyStrengthOptions());

        universe.Currencies.Should().HaveCount(CurrencyUniverseFactory.Default.Count);
        universe.Currencies.Select(c => c.Code).Should().Contain("USD");
    }

    [Fact]
    public void Factory_uses_configured_currencies_when_supplied()
    {
        var options = new CurrencyStrengthOptions
        {
            Universe =
            [
                new CurrencyConfig { Code = "USD", Tier = CurrencyTier.Major },
                new CurrencyConfig { Code = "EUR", Tier = CurrencyTier.Major }
            ]
        };

        var universe = CurrencyUniverseFactory.FromConfig(options);

        universe.Currencies.Select(c => c.Code).Should().BeEquivalentTo("USD", "EUR");
    }

    [Fact]
    public void Factory_rejects_null_options()
    {
        var act = () => CurrencyUniverseFactory.FromConfig(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Snapshot_create_defaults_a_null_narrative_to_empty()
    {
        var snapshot = CurrencyStrengthSnapshot.Create(Now, "{}", "{}", "{}", null!, SnapshotSource.CalendarAndAi,
            calendarKnownAt: Now.AddMinutes(-5));

        snapshot.AsOf.Should().Be(Now);
        snapshot.Source.Should().Be(SnapshotSource.CalendarAndAi);
        snapshot.Narrative.Should().BeEmpty();
        snapshot.CalendarKnownAt.Should().Be(Now.AddMinutes(-5));
    }
}
