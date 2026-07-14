using System.Security.Cryptography;
using Core;
using Core.Domain;
using Core.Time;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

public class AppUserTimeZoneTests
{
    private static AppUser NewUser() => AppUser.SelfRegister(2, new Email("tz@user.local"), "hash",
        RandomNumberGenerator.GetBytes(32), UserProfile.Create(fullName: "Zone User"),
        UserActivationState.Active);

    [Fact]
    public void SetTimeZone_stores_the_chosen_zone_on_the_profile()
    {
        var user = NewUser();
        user.SetTimeZone(TimeZoneId.From("Europe/Berlin"));
        user.Profile.TimeZone.Should().Be("Europe/Berlin");
    }

    [Fact]
    public void SetTimeZone_overwrites_a_previous_choice_without_dropping_other_profile_fields()
    {
        var user = NewUser();
        user.SetTimeZone(TimeZoneId.From("America/New_York"));
        user.SetTimeZone(TimeZoneId.From("Asia/Tokyo"));

        user.Profile.TimeZone.Should().Be("Asia/Tokyo");
        user.Profile.FullName.Should().Be("Zone User", "changing time zone must not wipe the rest of the profile");
    }

    [Fact]
    public void Profile_Create_validates_and_canonicalizes_the_time_zone()
    {
        UserProfile.Create(timeZone: "GMT Standard Time").TimeZone.Should().Be("Europe/London");
    }

    [Fact]
    public void Profile_Create_rejects_an_unknown_time_zone()
    {
        var act = () => UserProfile.Create(timeZone: "Not/AZone");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Profile_Create_leaves_time_zone_null_when_blank()
    {
        UserProfile.Create(fullName: "x").TimeZone.Should().BeNull();
    }
}
