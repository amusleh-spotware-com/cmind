using Core;
using FluentAssertions;
using Xunit;

namespace UnitTests.Compliance;

public class AuditChainTests
{
    private static readonly DateTimeOffset Now = new(2026, 07, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Hash_links_to_previous_and_is_reproducible()
    {
        var first = AuditLog.Record("Login", "User", Now);
        var h1 = first.ComputeHash(null);

        var second = AuditLog.Record("Start", "Instance", Now.AddSeconds(1));
        var h2 = second.ComputeHash(h1);

        second.PrevHash.Should().Be(h1);
        second.ExpectedHash(h1).Should().Be(h2);
    }

    [Fact]
    public void Changing_the_previous_hash_breaks_the_link()
    {
        var entry = AuditLog.Record("Start", "Instance", Now);
        var hash = entry.ComputeHash("PREV");

        entry.ExpectedHash("TAMPERED").Should().NotBe(hash);
    }

    [Fact]
    public void Different_content_yields_different_hash()
    {
        var a = AuditLog.Record("ActionA", "User", Now);
        var b = AuditLog.Record("ActionB", "User", Now);

        a.ComputeHash(null).Should().NotBe(b.ComputeHash(null));
    }
}
