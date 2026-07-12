using Core.Accounts;
using Core.Domain;
using Core.Options;
using FluentAssertions;
using Xunit;

namespace UnitTests.Accounts;

public sealed class BrokerAllowlistTests
{
    [Fact]
    public void BrokerName_trims_and_compares_case_insensitively()
    {
        new BrokerName("  Pepperstone  ").Value.Should().Be("Pepperstone");
        new BrokerName("PEPPERSTONE").Should().Be(new BrokerName("pepperstone"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BrokerName_rejects_blank(string value)
    {
        var act = () => new BrokerName(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(Core.Constants.DomainErrors.BrokerNameRequired);
    }

    [Fact]
    public void Empty_allowlist_is_unrestricted_and_allows_every_broker()
    {
        var allowlist = BrokerAllowlist.FromNames([]);

        allowlist.IsRestricted.Should().BeFalse();
        allowlist.Allows(new BrokerName("Anything")).Should().BeTrue();
    }

    [Fact]
    public void Restricted_allowlist_allows_listed_brokers_case_insensitively_and_blocks_others()
    {
        var allowlist = BrokerAllowlist.FromNames(["Pepperstone", "IC Markets"]);

        allowlist.IsRestricted.Should().BeTrue();
        allowlist.Allows(new BrokerName("pepperstone")).Should().BeTrue();
        allowlist.Allows(new BrokerName("IC MARKETS")).Should().BeTrue();
        allowlist.Allows(new BrokerName("Some Other Broker")).Should().BeFalse();
    }

    [Fact]
    public void FromNames_skips_blank_entries_and_deduplicates()
    {
        var allowlist = BrokerAllowlist.FromNames(["Pepperstone", "  ", "", "PEPPERSTONE"]);

        allowlist.Allowed.Should().ContainSingle();
    }

    [Fact]
    public void AccountsOptions_default_is_unrestricted()
    {
        var options = new AccountsOptions();

        BrokerAllowlist.FromNames(options.AllowedBrokers).IsRestricted.Should().BeFalse();
    }
}
