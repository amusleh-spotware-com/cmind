using System.Security.Cryptography;
using Core;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

// Regression: each user must own its OWN profile instance. Seeding several users at once with the shared
// UserProfile.Empty singleton made EF Core drop all but the first user's owned profile columns, producing a
// 23502 not-null violation on Profile_MarketingOptIn when saving the second user in the same batch.
public class AppUserProfileInstanceTests
{
    private static OwnerUser NewOwner(string email) =>
        OwnerUser.Create(new Email(email), "hash", RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void Two_freshly_created_users_do_not_share_the_same_profile_instance()
    {
        var first = NewOwner("first@owner.local");
        var second = NewOwner("second@owner.local");

        first.Profile.Should().NotBeSameAs(second.Profile);
    }

    [Fact]
    public void A_new_users_default_profile_is_empty_but_a_distinct_instance()
    {
        var user = NewOwner("default@owner.local");

        user.Profile.Should().Be(UserProfile.Empty, "an untouched profile is value-equal to the empty profile");
        user.Profile.Should().NotBeSameAs(UserProfile.Empty, "but it must not be the shared singleton reference");
    }
}
