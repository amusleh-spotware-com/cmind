using Microsoft.Extensions.Time.Testing;

namespace UnitTests;

internal static class TestClock
{
    public static readonly DateTimeOffset Now = new(2026, 07, 10, 12, 0, 0, TimeSpan.Zero);

    public static FakeTimeProvider Provider() => new(Now);
}
