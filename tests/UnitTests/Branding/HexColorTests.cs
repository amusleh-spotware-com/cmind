using Core.Branding;
using Core.Constants;
using Core.Domain;
using FluentAssertions;
using Xunit;

namespace UnitTests.Branding;

public class HexColorTests
{
    [Theory]
    [InlineData("#26C281")]
    [InlineData("#fff")]
    [InlineData("#ABC")]
    [InlineData("#000000")]
    public void Accepts_valid_hex(string value) => new HexColor(value).Value.Should().Be(value);

    [Theory]
    [InlineData("")]
    [InlineData("26C281")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    public void Rejects_invalid_hex(string value)
    {
        var act = () => new HexColor(value);
        act.Should().Throw<DomainException>().Which.Code.Should().Be(DomainErrors.BrandingColorInvalid);
    }
}
