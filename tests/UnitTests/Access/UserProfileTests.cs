using Core;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Access;

public class UserProfileTests
{
    [Fact]
    public void Empty_profile_is_valid_and_all_null()
    {
        var p = UserProfile.Empty;
        p.FullName.Should().BeNull();
        p.CountryCode.Should().BeNull();
        p.PhoneNumber.Should().BeNull();
        p.MarketingOptIn.Should().BeFalse();
    }

    [Fact]
    public void Create_normalizes_country_and_locale()
    {
        var p = UserProfile.Create(countryCode: "us", locale: "en-us");
        p.CountryCode.Should().Be("US");
        p.Locale.Should().Be("en-US");
    }

    [Fact]
    public void Create_trims_blank_text_to_null()
    {
        var p = UserProfile.Create(fullName: "   ", company: "  Acme  ");
        p.FullName.Should().BeNull();
        p.Company.Should().Be("Acme");
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("USA")]
    [InlineData("1")]
    public void Create_rejects_invalid_country(string code)
    {
        var act = () => UserProfile.Create(countryCode: code);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.ProfileCountryInvalid);
    }

    [Theory]
    [InlineData("+14155552671")]
    [InlineData("+442071838750")]
    public void Create_accepts_e164_phone(string phone)
        => UserProfile.Create(phoneNumber: phone).PhoneNumber.Should().Be(phone);

    [Theory]
    [InlineData("14155552671")]
    [InlineData("+0155")]
    [InlineData("phone")]
    public void Create_rejects_non_e164_phone(string phone)
    {
        var act = () => UserProfile.Create(phoneNumber: phone);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.ProfilePhoneInvalid);
    }

    [Fact]
    public void Create_rejects_invalid_locale()
    {
        var act = () => UserProfile.Create(locale: "not-a-locale-xyz");
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.ProfileLocaleInvalid);
    }

    [Fact]
    public void Create_rejects_overlong_text()
    {
        var act = () => UserProfile.Create(fullName: new string('a', 200));
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.ProfileTextTooLong);
    }
}
