using System.Security.Cryptography;
using Core;
using Core.Domain;
using Core.Localization;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

public class AppUserLocaleTests
{
    private static AppUser NewUser() => AppUser.SelfRegister(2, new Email("locale@user.local"), "hash",
        RandomNumberGenerator.GetBytes(32), UserProfile.Create(fullName: "Locale User"),
        UserActivationState.Active);

    [Fact]
    public void SetLocale_stores_the_chosen_culture_on_the_profile()
    {
        var user = NewUser();
        user.SetLocale(CultureName.From("ja"));
        user.Profile.Locale.Should().Be("ja");
    }

    [Fact]
    public void SetLocale_overwrites_a_previous_choice_without_dropping_other_profile_fields()
    {
        var user = NewUser();
        user.SetLocale(CultureName.From("de"));
        user.SetLocale(CultureName.From("ar"));

        user.Profile.Locale.Should().Be("ar");
        user.Profile.FullName.Should().Be("Locale User", "changing language must not wipe the rest of the profile");
    }
}
