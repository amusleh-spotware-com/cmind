using Infrastructure.Security;
using FluentAssertions;
using Xunit;

namespace UnitTests;

public class Argon2PasswordHasherTests
{
    [Fact]
    public void Verify_returns_true_for_correct_password()
    {
        var h = new Argon2PasswordHasher();
        var hash = h.Hash("p@ssword");
        h.Verify("p@ssword", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_returns_false_for_wrong_password()
    {
        var h = new Argon2PasswordHasher();
        var hash = h.Hash("p@ssword");
        h.Verify("wrong", hash).Should().BeFalse();
    }
}
