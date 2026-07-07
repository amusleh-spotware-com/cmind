using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class VersionInfoTests
{
    [Fact]
    public void Product_is_a_clean_semver_without_git_metadata()
    {
        VersionInfo.Product.Should().NotBeNullOrWhiteSpace();
        VersionInfo.Product.Should().NotContain("+");
        VersionInfo.Product.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }
}
