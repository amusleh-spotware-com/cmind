using Core.Constants;
using Core.Domain;
using Core.Localization;
using FluentAssertions;
using Xunit;

namespace UnitTests.Localization;

public class CultureNameTests
{
    [Fact]
    public void From_accepts_a_supported_culture()
    {
        var culture = CultureName.From("de");
        culture.Value.Should().Be("de");
        culture.IsRightToLeft.Should().BeFalse();
    }

    [Fact]
    public void From_normalizes_case_to_the_canonical_supported_form()
    {
        CultureName.From("PT-br").Value.Should().Be("pt-BR");
        CultureName.From("ZH-hans").Value.Should().Be("zh-Hans");
    }

    [Fact]
    public void From_trims_whitespace()
        => CultureName.From("  fr  ").Value.Should().Be("fr");

    [Fact]
    public void Arabic_culture_is_right_to_left()
        => CultureName.From("ar").IsRightToLeft.Should().BeTrue();

    [Theory]
    [InlineData("xx")]
    [InlineData("en-US")]
    [InlineData("klingon")]
    [InlineData("")]
    [InlineData(null)]
    public void From_rejects_unsupported_or_blank_cultures(string? value)
    {
        var act = () => CultureName.From(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.CultureNotSupported);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("ar", true)]
    [InlineData("nope", false)]
    [InlineData(null, false)]
    public void TryFrom_reports_success_only_for_supported_cultures(string? value, bool expected)
    {
        CultureName.TryFrom(value, out var culture).Should().Be(expected);
        if (expected) culture.Value.Should().NotBeNullOrEmpty();
    }
}
