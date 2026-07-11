using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.CopyTrading;

// Phase 4 marketplace: the provider listing aggregate. A listing needs a display name, carries the
// verified-live badge and fee, and toggles published state with a publish timestamp.
public sealed class CopyProviderListingTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 11, 12, 0, 0, TimeSpan.Zero);

    private static CopyProviderListing New(string name = "Alpha Strategy", bool verifiedLive = true)
        => CopyProviderListing.Create(UserId.New(), CopyProfileId.New(), name, "desc", new PerformanceFee(20), verifiedLive);

    [Fact]
    public void Create_requires_a_display_name()
    {
        var act = () => CopyProviderListing.Create(UserId.New(), CopyProfileId.New(), "  ", null, PerformanceFee.None, false);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_carries_the_fee_and_verified_live_badge()
    {
        var listing = New(verifiedLive: true);

        listing.PerformanceFeePercent.Should().Be(20);
        listing.VerifiedLive.Should().BeTrue();
        listing.Published.Should().BeFalse("a new listing is a draft until published");
        listing.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void Publish_marks_it_published_with_a_timestamp()
    {
        var listing = New();

        listing.Publish(Now);

        listing.Published.Should().BeTrue();
        listing.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Unpublish_hides_it_again()
    {
        var listing = New();
        listing.Publish(Now);

        listing.Unpublish();

        listing.Published.Should().BeFalse();
    }

    [Fact]
    public void UpdateDetails_replaces_the_display_fields()
    {
        var listing = New();

        listing.UpdateDetails("Beta Strategy", "new desc", new PerformanceFee(15), verifiedLive: false);

        listing.DisplayName.Should().Be("Beta Strategy");
        listing.Description.Should().Be("new desc");
        listing.PerformanceFeePercent.Should().Be(15);
        listing.VerifiedLive.Should().BeFalse();
    }
}
